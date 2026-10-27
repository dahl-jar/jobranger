import type { Env } from '../index.ts'

export const SITE_ORIGIN = 'https://jobranger.example.test'
export const RELAY = 'https://relay.jobranger.example.test'
const LIMITER_FAILURE = 'rate limit backend unavailable at 192.0.2.99'

export class FakeLimiter implements RateLimit {
  readonly #limit: number
  readonly #counts = new Map<string, number>()

  constructor(limit: number) {
    this.#limit = limit
  }

  async limit({ key }: RateLimitOptions): Promise<RateLimitOutcome> {
    const count = this.#counts.get(key) ?? 0
    if (count >= this.#limit) {
      return { success: false }
    }
    this.#counts.set(key, count + 1)
    return { success: true }
  }
}

export class BrokenLimiter implements RateLimit {
  async limit(): Promise<RateLimitOutcome> {
    throw new Error(LIMITER_FAILURE)
  }
}

const UNLIMITED = Number.MAX_SAFE_INTEGER

export function fakeEnv(overrides: Partial<Env> = {}): Env {
  return {
    PAIR_CLIENT: new FakeLimiter(UNLIMITED),
    PAIR_NETWORK: new FakeLimiter(UNLIMITED),
    SESSION_CLIENT: new FakeLimiter(UNLIMITED),
    SESSION_NETWORK: new FakeLimiter(UNLIMITED),
    HUB_CLIENT: new FakeLimiter(UNLIMITED),
    HUB_NETWORK: new FakeLimiter(UNLIMITED),
    SITE_ORIGIN,
    ...overrides,
  }
}

export function relayRequest(path: string, address: string, init: RequestInit = {}): Request {
  const headers = new Headers(init.headers)
  headers.set('CF-Connecting-IP', address)
  return new Request(`${RELAY}${path}`, { ...init, headers })
}
