import type { ConnectionState, UnpairReason } from '@/features/relay/connectionStore'
import { unpairCommand } from '@/features/relay/commands'
import type { Payload } from '@/features/relay/contract/payload.gen'
import { TicketRejectedError } from '@/features/relay/handleClient'
import type { StoredPairing } from '@/features/relay/keyStore'
import { clearLastSeen, readLastSeen, type SeenStorage, writeLastSeen } from '@/features/relay/lastSeen'
import type { PaceClock } from '@/features/relay/sendPacer'
import { type CloseReason, type SendProgress, type SessionEvent, SessionRunner } from '@/features/relay/sessionRunner'
import type { ConnectRelay, Presence } from '@/features/relay/transport'

export interface RelayClientDeps {
  loadPairing: () => Promise<StoredPairing | undefined>
  clearPairingOf: (channel: string) => Promise<void>
  fetchHandle: () => Promise<string>
  connect: ConnectRelay
  agent: string
  retryDelay: (previousRetryCount: number) => number
  parseAssembled?: (text: string) => Promise<Payload | undefined>
  seenStorage?: SeenStorage
  paceClock?: PaceClock
  onState: (state: ConnectionState) => void
  onMessage: (message: Payload) => void
  onDisconnect: () => void
  onSessionReset: () => void
  onReady: () => void
}

const SLOW_ATTEMPT_MS = 5_000

interface Paired {
  channel: string
  webStatic: CryptoKeyPair
  cliStatic: Uint8Array
}

function isSlow(startedAt: number): boolean {
  return Date.now() - startedAt >= SLOW_ATTEMPT_MS
}

function pairedFrom(pairing: StoredPairing | undefined): Paired | undefined {
  if (!pairing || pairing.pending || !pairing.cliStaticPublicKey) return undefined
  return { channel: pairing.channel, webStatic: pairing.webStatic, cliStatic: pairing.cliStaticPublicKey }
}

export class RelayClient {
  readonly #deps: RelayClientDeps
  #paired: Paired | undefined
  #runner: SessionRunner | undefined
  #retries = 0
  #retryTimer: ReturnType<typeof setTimeout> | undefined
  #handleRequest: Promise<string> | undefined
  #stored: Promise<StoredPairing | undefined> | undefined
  #cliOnline = false
  #networkDown = false
  #stopped = false

  constructor(deps: RelayClientDeps) {
    this.#deps = deps
  }

  async start(): Promise<void> {
    const pairing = await this.#loadStored()
    if (this.#stopped) return
    this.#paired = pairedFrom(pairing)
    if (!this.#paired) {
      clearLastSeen(this.#deps.seenStorage)
      await this.#clearStored()
      this.#deps.onState({ kind: 'unpaired', reason: undefined })
      return
    }
    await this.#connect()
  }

  async send(message: Payload, onProgress?: SendProgress): Promise<void> {
    if (!this.#runner) throw new Error('session is not ready')
    await this.#runner.send(message, onProgress)
  }

  retryNow(): void {
    if (this.#retryTimer === undefined) return
    this.#clearRetry()
    void this.#connect()
  }

  pauseForNetwork(): void {
    if (this.#stopped || this.#networkDown) return
    this.#networkDown = true
    const runner = this.#detach()
    this.#deps.onDisconnect()
    this.#deps.onState({ kind: 'networkOffline' })
    void runner?.stop()
  }

  resumeForNetwork(): void {
    if (!this.#networkDown) return
    this.#networkDown = false
    this.#retries = 0
    void this.#connect()
  }

  async unpair(reason?: UnpairReason): Promise<void> {
    if (reason === undefined) await this.send(unpairCommand()).catch(() => undefined)
    await this.stop()
    clearLastSeen(this.#deps.seenStorage)
    this.#deps.onState({ kind: 'unpaired', reason })
    await this.#handleRequest?.catch(() => undefined)
    await this.#clearStored()
  }

  async stop(): Promise<void> {
    this.#stopped = true
    await this.#detach()?.stop()
  }

  #loadStored(): Promise<StoredPairing | undefined> {
    this.#stored ??= this.#deps.loadPairing().catch(() => undefined)
    return this.#stored
  }

  async #clearStored(): Promise<void> {
    const pairing = await this.#loadStored()
    if (pairing) await this.#deps.clearPairingOf(pairing.channel).catch(() => undefined)
  }

  #clearRetry(): void {
    clearTimeout(this.#retryTimer)
    this.#retryTimer = undefined
  }

  #detach(): SessionRunner | undefined {
    this.#clearRetry()
    const runner = this.#runner
    this.#runner = undefined
    return runner
  }

  async #connect(): Promise<void> {
    const paired = this.#paired
    if (this.#stopped || !paired) return
    if (this.#networkDown) {
      this.#deps.onState({ kind: 'networkOffline' })
      return
    }
    this.#deps.onState({ kind: 'connecting' })
    const startedAt = Date.now()
    const handle = await this.#requestHandle(startedAt)
    if (handle === undefined || this.#stopped || this.#networkDown) return
    await this.#startSession(paired, handle, startedAt)
  }

  async #requestHandle(startedAt: number): Promise<string | undefined> {
    const request = this.#deps.fetchHandle()
    this.#handleRequest = request
    try {
      return await request
    } catch (error: unknown) {
      if (error instanceof TicketRejectedError) await this.unpair('ticket')
      else this.#retry(isSlow(startedAt))
      return undefined
    }
  }

  async #startSession(paired: Paired, handle: string, startedAt: number): Promise<void> {
    this.#cliOnline = false
    const runner = new SessionRunner({
      ...paired,
      agent: this.#deps.agent,
      onEvent: (event) => this.#onSession(runner, event),
      ...(this.#deps.parseAssembled && { parseAssembled: this.#deps.parseAssembled }),
      ...(this.#deps.paceClock && { paceClock: this.#deps.paceClock }),
    })
    this.#runner = runner
    try {
      await runner.start(this.#watchPresence(runner), handle)
    } catch {
      if (this.#runner !== runner) return
      this.#runner = undefined
      this.#retry(isSlow(startedAt))
      return
    }
    if (this.#runner !== runner) await runner.stop()
  }

  #retry(attemptWasSlow: boolean): void {
    if (this.#stopped || this.#networkDown) return
    let delay = this.#nextDelay()
    if (delay === 0 && attemptWasSlow) delay = this.#nextDelay()
    if (delay === 0) {
      void this.#connect()
      return
    }
    this.#deps.onState({ kind: 'relayDown', retryAt: Date.now() + delay })
    this.#retryTimer = setTimeout(() => {
      this.#retryTimer = undefined
      void this.#connect()
    }, delay)
  }

  #nextDelay(): number {
    const delay = this.#deps.retryDelay(this.#retries)
    this.#retries += 1
    return delay
  }

  #watchPresence(runner: SessionRunner): ConnectRelay {
    return (handle, events) =>
      this.#deps.connect(handle, {
        onEnvelope: (text) => events.onEnvelope(text),
        onPresence: (presence) => {
          events.onPresence(presence)
          if (this.#runner === runner) this.#onPresence(presence)
        },
        onClose: () => events.onClose(),
      })
  }

  #onPresence(presence: Presence): void {
    const wasOnline = this.#cliOnline
    this.#cliOnline = presence.cli_online
    if (presence.cli_online) return
    if (wasOnline) {
      writeLastSeen(new Date().toISOString(), this.#deps.seenStorage)
      this.#deps.onDisconnect()
    }
    this.#deps.onState({ kind: 'machineOffline', lastSeen: readLastSeen(this.#deps.seenStorage) })
  }

  #onSession(runner: SessionRunner, event: SessionEvent): void {
    if (this.#runner !== runner || this.#stopped) return
    switch (event.kind) {
      case 'handshaking':
        this.#deps.onState({ kind: 'handshaking' })
        return
      case 'ready':
        this.#retries = 0
        this.#deps.onState({ kind: 'ready' })
        this.#deps.onReady()
        return
      case 'message':
        writeLastSeen(new Date().toISOString(), this.#deps.seenStorage)
        this.#deps.onMessage(event.payload)
        return
      case 'closed':
        this.#onClosed(event.reason)
        return
      case 'failed':
        this.#deps.onDisconnect()
        this.#deps.onState({ kind: 'failed', reason: event.reason })
        void this.stop()
        return
      default:
        return
    }
  }

  #onClosed(reason: CloseReason): void {
    if (reason === 'unknown_session') this.#deps.onSessionReset()
    else this.#deps.onDisconnect()
    if (reason !== 'connection') {
      this.#deps.onState({ kind: 'handshaking' })
      return
    }
    this.#runner = undefined
    this.#retry(false)
  }
}
