import { describe, expect, it } from 'vitest'
import { retryDelayMs } from '@/features/relay/retryPolicy'

describe('retryDelayMs', () => {
  it.each([
    [0, 0],
    [1, 2000],
    [2, 10000],
    [3, 30000],
  ])('should wait the SignalR default step after %i retries', (retries, delay) => {
    expect(retryDelayMs(retries, () => 0)).toBe(delay)
  })

  it('should keep retrying about every 60 seconds after 4 retries', () => {
    expect(retryDelayMs(4, () => 0)).toBe(60000)
  })

  it('should add up to 5 seconds of jitter to the steady delay', () => {
    expect(retryDelayMs(4, () => 0.999)).toBe(64995)
  })
})
