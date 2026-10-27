import { agentName } from '@/features/pairing/agentName'
import { type ClaimedPairing, type ClaimResult, claimPairing } from '@/features/pairing/claimPairing'
import { formatPairCode, normalizePairCode, validatePairCode } from '@/features/pairing/pairCode'
import type { EndReason, PairingEvent } from '@/features/pairing/pairingMachine'
import { unpairCommand } from '@/features/relay/commands'
import { clearLastSeen } from '@/features/relay/lastSeen'
import { connectHub } from '@/features/relay/hubTransport'
import { clearPairing, generateWebKeys, loadPairing, probeStorage, savePairing, type StoredPairing } from '@/features/relay/keyStore'
import { type SessionEvent, SessionRunner } from '@/features/relay/sessionRunner'
import type { ConnectRelay } from '@/features/relay/transport'

export interface PairingDeps {
  claim: (code: string) => Promise<ClaimResult>
  connect: ConnectRelay
  agent: string
}

export function defaultPairingDeps(): PairingDeps {
  return { claim: claimPairing, connect: connectHub(), agent: agentName(navigator.userAgent) }
}

export class PairingFlow {
  readonly #deps: PairingDeps
  readonly #dispatch: (event: PairingEvent) => void
  #runner: SessionRunner | undefined
  #stored: StoredPairing | undefined
  #ready = false
  #disposed = false
  #work: Promise<void> = Promise.resolve()

  constructor(deps: PairingDeps, dispatch: (event: PairingEvent) => void) {
    this.#deps = deps
    this.#dispatch = dispatch
  }

  #emit(event: PairingEvent): void {
    if (!this.#disposed) this.#dispatch(event)
  }

  #queue<T>(task: () => Promise<T>): Promise<T> {
    const run = this.#work.then(task, task)
    this.#work = run.then(
      () => undefined,
      () => undefined,
    )
    return run
  }

  load(ended?: EndReason): Promise<void> {
    return this.#queue(async () => {
      const pairing = await loadPairing().catch(() => undefined)
      const complete = pairing !== undefined && !pairing.pending && pairing.cliStaticPublicKey !== undefined
      if (pairing !== undefined && !complete) await clearPairing().catch(() => undefined)
      this.#emit({ type: 'loaded', paired: complete, ended })
    })
  }

  async submit(raw: string): Promise<void> {
    const code = normalizePairCode(raw)
    const error = validatePairCode(code)
    if (error !== undefined) {
      this.#emit({ type: 'rejected', error })
      return
    }
    this.#emit({ type: 'submitted' })
    if (!(await probeStorage())) {
      this.#emit({ type: 'rejected', error: 'storage' })
      return
    }
    const result = await this.#deps.claim(code)
    if (result.kind !== 'ok') {
      this.#emit({ type: 'rejected', error: result.kind })
      return
    }
    const runner = await this.#queue(() => this.#savePending(result, formatPairCode(code)))
    if (runner) await this.#startRunner(runner, result.handle)
  }

  async #savePending(claimed: ClaimedPairing, pairCode: string): Promise<SessionRunner | undefined> {
    void this.#runner?.stop()
    const webStatic = await generateWebKeys()
    const stored: StoredPairing = {
      channel: claimed.channel,
      ticket: claimed.ticket,
      ticketExpiresAt: claimed.ticketExpiresAt,
      pending: true,
      webStatic,
      pairedAt: new Date().toISOString(),
    }
    try {
      await savePairing(stored)
    } catch {
      this.#emit({ type: 'rejected', error: 'storage' })
      return undefined
    }
    this.#stored = stored
    this.#ready = false
    this.#runner = this.#createRunner(claimed.channel, webStatic, pairCode)
    this.#emit({ type: 'claimed' })
    return this.#runner
  }

  #createRunner(channel: string, webStatic: CryptoKeyPair, pairCode: string): SessionRunner {
    const runner: SessionRunner = new SessionRunner({
      channel,
      webStatic,
      agent: this.#deps.agent,
      cliStatic: undefined,
      pairCode,
      onEvent: (event) => {
        void this.#queue(async () => {
          if (this.#runner === runner) await this.#onSession(event)
        })
      },
    })
    return runner
  }

  async #startRunner(runner: SessionRunner, handle: string): Promise<void> {
    try {
      await runner.start(this.#deps.connect, handle)
    } catch {
      await this.#queue(() => this.#onSession({ kind: 'closed', reason: 'connection' }))
    }
  }

  async #forget(): Promise<void> {
    const runner = this.#runner
    this.#runner = undefined
    this.#stored = undefined
    this.#ready = false
    void runner?.stop()
    clearLastSeen()
    await clearPairing().catch(() => undefined)
  }

  async #onSession(event: SessionEvent): Promise<void> {
    if (event.kind === 'message' && event.payload.type === 'unpaired') {
      await this.#forget()
      this.#emit({ type: 'revoked' })
      return
    }
    if (event.kind === 'ready' && this.#stored) {
      const paired: StoredPairing = { ...this.#stored, pending: false, cliStaticPublicKey: event.cliStatic }
      try {
        await savePairing(paired)
      } catch {
        await this.#forget()
        this.#emit({ type: 'rejected', error: 'storage' })
        return
      }
      this.#stored = paired
      this.#ready = true
    }
    if ((event.kind === 'closed' || event.kind === 'failed') && !this.#ready) await this.#forget()
    this.#emit({ type: 'session', event })
  }

  answer(match: boolean): void {
    this.#runner?.answerWords(match)
    if (match) this.#emit({ type: 'answered' })
  }

  unpair(): Promise<void> {
    return this.#queue(async () => {
      if (this.#runner && this.#ready) {
        await this.#runner.send(unpairCommand()).catch(() => undefined)
      }
      await this.#forget()
      this.#emit({ type: 'unpaired' })
    })
  }

  dispose(): Promise<void> {
    this.#disposed = true
    return this.#queue(async () => {
      await this.#runner?.stop()
      if (this.#stored && !this.#ready) await clearPairing()
    })
  }
}
