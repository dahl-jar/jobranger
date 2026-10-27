import { CONTRACT_VERSION } from '@/features/relay/contract/limits'
import type { ErrorMessage, Payload, Saved, Unpair } from '@/features/relay/contract/payload.gen'

export const COMMAND_TIMEOUT_MS = 15_000

const TIMEOUT_TEXT = 'no answer from your machine.'
const DISCONNECTED_TEXT = 'connection lost.'

export type Command = Extract<Payload, { id: string }>
export type CommandDraft = Command extends infer Each ? (Each extends Command ? Omit<Each, 'id'> : never) : never
type CommandErrorCode = ErrorMessage['code'] | 'timeout' | 'disconnected'

export class CommandError extends Error {
  readonly code: CommandErrorCode
  readonly field: string | null

  constructor(code: CommandErrorCode, message: string, field: string | null = null) {
    super(message)
    this.name = 'CommandError'
    this.code = code
    this.field = field
  }
}

export function unpairCommand(): Unpair {
  return { type: 'unpair', v: CONTRACT_VERSION, id: crypto.randomUUID() }
}

export function disconnectedError(): CommandError {
  return new CommandError('disconnected', DISCONNECTED_TEXT)
}

type Delivery = 'first' | 'held' | 'resent'

interface Waiting {
  command: Command
  delivery: Delivery
  resolve: (saved: Saved) => void
  reject: (error: CommandError) => void
  timer: ReturnType<typeof setTimeout> | undefined
}

export class CommandTracker {
  readonly #deliver: (command: Command) => Promise<void>
  readonly #waiting = new Map<string, Waiting>()

  constructor(deliver: (command: Command) => Promise<void>) {
    this.#deliver = deliver
  }

  send(draft: CommandDraft): Promise<Saved> {
    const id = crypto.randomUUID()
    const command: Command = { ...draft, id }
    const reply = new Promise<Saved>((resolve, reject) => {
      this.#waiting.set(id, { command, delivery: 'first', resolve, reject, timer: undefined })
    })
    this.#dispatch(command)
    return reply
  }

  settle(reply: Saved | ErrorMessage): void {
    if (reply.ref === null) return
    const waiting = this.#finish(reply.ref)
    if (reply.type === 'saved') waiting?.resolve(reply)
    else waiting?.reject(new CommandError(reply.code, reply.message, reply.field))
  }

  hold(): void {
    for (const [id, waiting] of this.#waiting) {
      if (waiting.delivery === 'resent') {
        this.#finish(id)?.reject(disconnectedError())
        continue
      }
      clearTimeout(waiting.timer)
      waiting.timer = undefined
      waiting.delivery = 'held'
    }
  }

  resendHeld(): void {
    for (const waiting of this.#waiting.values()) {
      if (waiting.delivery !== 'held') continue
      waiting.delivery = 'resent'
      this.#dispatch(waiting.command)
    }
  }

  failAll(): void {
    for (const id of [...this.#waiting.keys()]) this.#finish(id)?.reject(disconnectedError())
  }

  #dispatch(command: Command): void {
    this.#deliver(command).then(
      () => this.#startTimer(command.id),
      () => this.#finish(command.id)?.reject(disconnectedError()),
    )
  }

  #startTimer(id: string): void {
    const waiting = this.#waiting.get(id)
    if (!waiting || waiting.delivery === 'held') return
    waiting.timer = setTimeout(() => this.#finish(id)?.reject(new CommandError('timeout', TIMEOUT_TEXT)), COMMAND_TIMEOUT_MS)
  }

  #finish(id: string): Waiting | undefined {
    const waiting = this.#waiting.get(id)
    this.#waiting.delete(id)
    clearTimeout(waiting?.timer)
    return waiting
  }
}
