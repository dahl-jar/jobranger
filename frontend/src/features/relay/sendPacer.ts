import { GCM_TAG_BYTES } from '@/features/relay/contract/limits'

export interface PaceClock {
  now(): number
  wait(ms: number): Promise<void>
}

const MS_PER_SECOND = 1000
const BASE64_CHARS_PER_BLOCK = 4
const BYTES_PER_BASE64_BLOCK = 3
const RELAY_FRAMES_PER_SECOND = 20
const RELAY_FRAME_BURST = 40
const RELAY_CHARS_PER_SECOND = 262_144
const RELAY_CHAR_BURST = 524_288
const PACE_SHARE = 0.8

const systemPaceClock: PaceClock = {
  now: () => performance.now(),
  wait: (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
}

export function sealedChars(plaintextBytes: number): number {
  return Math.ceil(((plaintextBytes + GCM_TAG_BYTES) * BASE64_CHARS_PER_BLOCK) / BYTES_PER_BASE64_BLOCK)
}

class Bucket {
  readonly #capacity: number
  readonly #perMs: number
  #tokens: number
  #updatedAt: number

  constructor(relayBurst: number, relayPerSecond: number, now: number) {
    this.#capacity = relayBurst - relayPerSecond
    this.#perMs = (relayPerSecond * PACE_SHARE) / MS_PER_SECOND
    this.#tokens = this.#capacity
    this.#updatedAt = now
  }

  refill(now: number): void {
    this.#tokens = Math.min(this.#capacity, this.#tokens + (now - this.#updatedAt) * this.#perMs)
    this.#updatedAt = now
  }

  waitMs(count: number): number {
    return Math.max(0, (count - this.#tokens) / this.#perMs)
  }

  take(count: number): void {
    this.#tokens -= count
  }
}

export class SendPacer {
  readonly #clock: PaceClock
  readonly #frames: Bucket
  readonly #chars: Bucket

  constructor(clock: PaceClock = systemPaceClock) {
    this.#clock = clock
    this.#frames = new Bucket(RELAY_FRAME_BURST, RELAY_FRAMES_PER_SECOND, clock.now())
    this.#chars = new Bucket(RELAY_CHAR_BURST, RELAY_CHARS_PER_SECOND, clock.now())
  }

  take(chars: number): Promise<void> | undefined {
    const now = this.#clock.now()
    this.#frames.refill(now)
    this.#chars.refill(now)
    const delay = Math.max(this.#frames.waitMs(1), this.#chars.waitMs(chars))
    this.#frames.take(1)
    this.#chars.take(chars)
    return delay > 0 ? this.#clock.wait(delay) : undefined
  }
}
