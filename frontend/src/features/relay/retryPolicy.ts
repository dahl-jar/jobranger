const FIRST_DELAYS_MS: readonly number[] = [0, 2000, 10000, 30000]
const STEADY_DELAY_MS = 60000
const JITTER_MS = 5000

export function retryDelayMs(previousRetryCount: number, random: () => number = Math.random): number {
  return FIRST_DELAYS_MS[previousRetryCount] ?? STEADY_DELAY_MS + Math.floor(random() * JITTER_MS)
}
