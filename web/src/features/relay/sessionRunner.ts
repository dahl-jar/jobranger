import { ChunkAssembler, parseAssembled, splitMessage } from '@/features/relay/chunks'
import type { Envelope } from '@/features/relay/contract/envelope.gen'
import { CONTRACT_VERSION, PAIR_CONFIRM_TIMEOUT_SECONDS, STEP_TIMEOUT_SECONDS } from '@/features/relay/contract/limits'
import type { Payload } from '@/features/relay/contract/payload.gen'
import { parseEnvelope, parsePayload } from '@/features/relay/contract/validate'
import { encodeHello, type IncomingHello, type OutgoingHello, parseHello } from '@/features/relay/hello'
import {
  answerOffer,
  beginHandshake,
  finishHandshake,
  HandshakeError,
  type OfferedHandshake,
  pairTag,
  type PendingHandshake,
  type Session,
} from '@/features/relay/session'
import { type PaceClock, sealedChars, SendPacer } from '@/features/relay/sendPacer'
import type { ConnectRelay, Presence, RelayEvents, RelayTransport } from '@/features/relay/transport'

const MS_PER_SECOND = 1000
const STEP_TIMEOUT_MS = STEP_TIMEOUT_SECONDS * MS_PER_SECOND
const PAIR_CONFIRM_TIMEOUT_MS = PAIR_CONFIRM_TIMEOUT_SECONDS * MS_PER_SECOND
const HELLO_SEQ = 0

export const RESTART_BACKOFF_MS: readonly number[] = [1000, 2000, 4000, 8000, 16000]

export type CloseReason =
  | 'abort'
  | 'unknown_session'
  | 'timeout'
  | 'words_timeout'
  | 'no_answer'
  | 'rejected'
  | 'broken'
  | 'connection'
  | 'offline'
export type SendProgress = (sent: number, count: number) => void

export type FailReason = 'handshake' | 'cli_key' | 'unknown_device'

export type SessionEvent =
  | { kind: 'handshaking' }
  | { kind: 'confirming'; words: string[] }
  | { kind: 'ready'; cliStatic: Uint8Array }
  | { kind: 'message'; payload: Payload }
  | { kind: 'offline' }
  | { kind: 'closed'; reason: CloseReason }
  | { kind: 'failed'; reason: FailReason }

export interface SessionRunnerOptions {
  channel: string
  webStatic: CryptoKeyPair
  agent: string
  cliStatic: Uint8Array | undefined
  pairCode?: string | undefined
  onEvent: (event: SessionEvent) => void
  parseAssembled?: (text: string) => Promise<Payload | undefined>
  paceClock?: PaceClock
}

type Phase =
  | { name: 'idle' }
  | { name: 'commit'; sid: string; pending: PendingHandshake }
  | { name: 'reveal'; sid: string; offered: OfferedHandshake }
  | { name: 'words'; sid: string; session: Session; confirm: OutgoingHello }
  | { name: 'firstFrame'; sid: string; session: Session }
  | { name: 'ready'; sid: string; session: Session }
  | { name: 'done' }

type ActivePhase = Exclude<Phase, { name: 'idle' } | { name: 'done' }>

function isActive(phase: Phase): phase is ActivePhase {
  return phase.name !== 'idle' && phase.name !== 'done'
}

export class SessionRunner implements RelayEvents {
  readonly #options: SessionRunnerOptions
  readonly #pairing: boolean
  readonly #pacer: SendPacer
  #phase: Phase = { name: 'idle' }
  #transport: RelayTransport | undefined
  #cliOnline = false
  #restarts = 0
  #tasks: Promise<void> = Promise.resolve()
  #outgoing: Promise<void> = Promise.resolve()
  #stepTimer: ReturnType<typeof setTimeout> | undefined
  #restartTimer: ReturnType<typeof setTimeout> | undefined
  #deadline: ReturnType<typeof setTimeout> | undefined
  #assembler = new ChunkAssembler()

  constructor(options: SessionRunnerOptions) {
    this.#options = options
    this.#pairing = options.cliStatic === undefined
    this.#pacer = new SendPacer(options.paceClock)
    if (this.#pairing && options.pairCode === undefined) throw new Error('pairing needs the claimed code')
  }

  async start(connect: ConnectRelay, handle: string): Promise<void> {
    this.#transport = await connect(handle, this)
    if (this.#pairing) this.#deadline = setTimeout(() => this.#enqueue(() => this.#expireClaim()), PAIR_CONFIRM_TIMEOUT_MS)
    this.#enqueue(() => this.#begin())
  }

  async #expireClaim(): Promise<void> {
    const phase = this.#phase
    if (phase.name === 'idle' || phase.name === 'commit') await this.#close('no_answer', phase.name === 'commit')
  }

  onPresence(presence: Presence): void {
    this.#enqueue(() => this.#applyPresence(presence.cli_online))
  }

  onEnvelope(text: string): void {
    this.#enqueue(() => this.#receive(text))
  }

  onClose(): void {
    this.#enqueue(async () => {
      this.#transport = undefined
      if (this.#phase.name !== 'done') await this.#close('connection', false)
    })
  }

  answerWords(match: boolean): void {
    this.#enqueue(async () => {
      const phase = this.#phase
      if (phase.name !== 'words') return
      if (!match) {
        await this.#close('rejected', true)
        return
      }
      this.#phase = { name: 'firstFrame', sid: phase.sid, session: phase.session }
      await this.#sendHello(phase.sid, phase.confirm)
    })
  }

  async send(payload: Payload, onProgress?: SendProgress): Promise<void> {
    const phase = this.#phase
    const transport = this.#transport
    if (phase.name !== 'ready' || !transport) throw new Error('session is not ready')
    const parts = splitMessage(payload)
    const sending = this.#outgoing.then(async () => {
      for (const [index, part] of parts.entries()) {
        const plaintext = new TextEncoder().encode(JSON.stringify(part))
        const pacing = this.#pacer.take(sealedChars(plaintext.length))
        if (pacing) await pacing
        const frame = await phase.session.seal(plaintext)
        await transport.send(this.#envelope(phase.sid, frame.seq, 'data', frame.ciphertext))
        if (this.#phase === phase) onProgress?.(index + 1, parts.length)
      }
    })
    this.#outgoing = sending.catch(() => undefined)
    await sending
  }

  async stop(): Promise<void> {
    this.#enqueue(async () => {
      const phase = this.#phase
      this.#clearTimers()
      this.#phase = { name: 'done' }
      if (isActive(phase)) await this.#sendHello(phase.sid, { t: 'abort' }).catch(() => undefined)
      await this.#transport?.stop().catch(() => undefined)
    })
    await this.#tasks
  }

  #enqueue(task: () => Promise<void>): void {
    this.#tasks = this.#tasks.then(task).catch(async () => {
      if (isActive(this.#phase)) await this.#close('broken', true)
    })
  }

  #emit(event: SessionEvent): void {
    this.#options.onEvent(event)
  }

  #envelope(sid: string, seq: number, kind: Envelope['kind'], ciphertext: string): Envelope {
    return kind === 'hello'
      ? { v: CONTRACT_VERSION, channel: this.#options.channel, sid, seq: HELLO_SEQ, kind, ciphertext }
      : { v: CONTRACT_VERSION, channel: this.#options.channel, sid, seq, kind, ciphertext }
  }

  async #sendHello(sid: string, step: OutgoingHello): Promise<void> {
    await this.#transport?.send(this.#envelope(sid, HELLO_SEQ, 'hello', encodeHello(step)))
  }

  #clearTimers(): void {
    clearTimeout(this.#stepTimer)
    clearTimeout(this.#restartTimer)
    clearTimeout(this.#deadline)
    this.#stepTimer = undefined
    this.#restartTimer = undefined
    this.#deadline = undefined
  }

  #armStep(sid: string, delayMs: number, reason: CloseReason): void {
    clearTimeout(this.#stepTimer)
    this.#stepTimer = setTimeout(() => {
      this.#enqueue(async () => {
        if (isActive(this.#phase) && this.#phase.sid === sid) await this.#close(reason, true)
      })
    }, delayMs)
  }

  #shutdown(): void {
    this.#clearTimers()
    this.#phase = { name: 'done' }
    void this.#transport?.stop().catch(() => undefined)
  }

  async #begin(): Promise<void> {
    if (this.#phase.name !== 'idle' || !this.#transport || !this.#cliOnline) return
    this.#emit({ kind: 'handshaking' })
    const code = this.#options.pairCode
    const tag = this.#pairing && code !== undefined ? await pairTag(code, this.#options.channel) : undefined
    const { commit, pending } = await beginHandshake({
      webStatic: this.#options.webStatic,
      agent: this.#options.agent,
      ...(tag === undefined ? {} : { pairTag: tag }),
    })
    this.#phase = { name: 'commit', sid: pending.sid, pending }
    this.#assembler = new ChunkAssembler()
    this.#armStep(pending.sid, STEP_TIMEOUT_MS, 'timeout')
    await this.#sendHello(pending.sid, commit)
  }

  async #applyPresence(online: boolean): Promise<void> {
    const wasOnline = this.#cliOnline
    this.#cliOnline = online
    if (online && !wasOnline) {
      this.#restarts = 0
      await this.#begin()
      return
    }
    if (online || !wasOnline) return
    if (this.#pairing) {
      if (isActive(this.#phase)) await this.#close('offline', false)
      return
    }
    await this.#drop(false)
    this.#emit({ kind: 'offline' })
  }

  async #receive(text: string): Promise<void> {
    const phase = this.#phase
    if (!isActive(phase)) return
    let envelope: Envelope
    try {
      envelope = parseEnvelope(text)
    } catch {
      await this.#close('broken', true)
      return
    }
    if (envelope.channel !== this.#options.channel || envelope.sid !== phase.sid) return
    if (envelope.kind === 'hello') await this.#onHello(phase, envelope.ciphertext)
    else await this.#onData(phase, envelope.seq, envelope.ciphertext)
  }

  async #onHello(phase: ActivePhase, ciphertext: string): Promise<void> {
    let step: IncomingHello
    try {
      step = parseHello(ciphertext)
    } catch {
      await this.#close('broken', true)
      return
    }
    if (step.t === 'abort' || step.t === 'unknown_session') {
      await this.#close(step.t, false)
    } else if (phase.name === 'commit' && step.t === 'unknown_device') {
      await this.#fail('unknown_device', false)
    } else if (phase.name === 'commit' && step.t === 'offer') {
      await this.#onOffer(phase.pending, step)
    } else if (phase.name === 'reveal' && step.t === 'confirm') {
      await this.#onConfirm(phase.offered, step)
    } else {
      await this.#close('broken', true)
    }
  }

  async #onOffer(pending: PendingHandshake, offer: Extract<IncomingHello, { t: 'offer' }>): Promise<void> {
    let answer: Awaited<ReturnType<typeof answerOffer>>
    try {
      answer = await answerOffer(pending, offer, this.#options.cliStatic)
    } catch (error: unknown) {
      if (error instanceof HandshakeError && error.reason === 'cli_key') await this.#fail('cli_key', true)
      else await this.#close('broken', true)
      return
    }
    this.#phase = { name: 'reveal', sid: pending.sid, offered: answer.offered }
    this.#armStep(pending.sid, STEP_TIMEOUT_MS, 'timeout')
    await this.#sendHello(pending.sid, answer.reveal)
  }

  async #onConfirm(offered: OfferedHandshake, confirm: Extract<IncomingHello, { t: 'confirm' }>): Promise<void> {
    let finished: Awaited<ReturnType<typeof finishHandshake>>
    try {
      finished = await finishHandshake(offered, confirm)
    } catch {
      await this.#fail('handshake', true)
      return
    }
    if (this.#pairing) {
      this.#phase = { name: 'words', sid: offered.sid, session: finished.session, confirm: finished.confirm }
      this.#armStep(offered.sid, PAIR_CONFIRM_TIMEOUT_MS, 'words_timeout')
      this.#emit({ kind: 'confirming', words: finished.words })
      return
    }
    clearTimeout(this.#stepTimer)
    this.#restarts = 0
    this.#phase = { name: 'ready', sid: offered.sid, session: finished.session }
    await this.#sendHello(offered.sid, finished.confirm)
    this.#emit({ kind: 'ready', cliStatic: finished.session.cliStatic })
  }

  async #onData(phase: ActivePhase, seq: number, ciphertext: string): Promise<void> {
    if (phase.name !== 'firstFrame' && phase.name !== 'ready') {
      await this.#close('broken', true)
      return
    }
    let payload: Payload
    try {
      const plaintext = await phase.session.open(seq, ciphertext)
      payload = parsePayload(new TextDecoder('utf-8', { fatal: true }).decode(plaintext))
    } catch {
      await this.#close('broken', true)
      return
    }
    if (phase.name === 'firstFrame') {
      clearTimeout(this.#stepTimer)
      this.#phase = { name: 'ready', sid: phase.sid, session: phase.session }
      this.#emit({ kind: 'ready', cliStatic: phase.session.cliStatic })
    }
    const message = payload.type === 'chunk' ? await this.#assemble(payload) : payload
    if (message) this.#emit({ kind: 'message', payload: message })
  }

  async #assemble(chunk: Extract<Payload, { type: 'chunk' }>): Promise<Payload | undefined> {
    const text = this.#assembler.acceptText(chunk, Date.now())
    if (text === undefined) return undefined
    const parse = this.#options.parseAssembled
    return parse ? parse(text) : parseAssembled(text)
  }

  async #drop(sendAbort: boolean): Promise<void> {
    const phase = this.#phase
    this.#clearTimers()
    if (!isActive(phase)) return
    if ('session' in phase) phase.session.close()
    this.#phase = { name: 'idle' }
    if (sendAbort) await this.#sendHello(phase.sid, { t: 'abort' }).catch(() => undefined)
  }

  async #fail(reason: FailReason, sendAbort: boolean): Promise<void> {
    await this.#drop(sendAbort)
    this.#shutdown()
    this.#emit({ kind: 'failed', reason })
  }

  async #close(reason: CloseReason, sendAbort: boolean): Promise<void> {
    await this.#drop(sendAbort)
    this.#emit({ kind: 'closed', reason })
    if (this.#pairing || reason === 'connection') {
      this.#shutdown()
      return
    }
    const delay = RESTART_BACKOFF_MS[this.#restarts]
    if (delay === undefined) {
      this.#shutdown()
      this.#emit({ kind: 'failed', reason: 'handshake' })
      return
    }
    this.#restarts += 1
    this.#restartTimer = setTimeout(() => this.#enqueue(() => this.#begin()), delay)
  }
}
