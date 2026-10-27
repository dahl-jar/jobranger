import { describe, expect, it } from 'vitest'
import { formatTokens } from '@/shared/format/tokens'

describe('formatTokens', () => {
  it.each([
    { count: 850, text: '850' },
    { count: 1_000, text: '1k' },
    { count: 41_400, text: '41k' },
    { count: 999_600, text: '1M' },
    { count: 1_240_000, text: '1.2M' },
    { count: 1_250_000, text: '1.3M' },
    { count: 1_950_000, text: '2M' },
    { count: 999_960_000, text: '1B' },
    { count: 1_250_000_000, text: '1.3B' },
    { count: Number.NaN, text: '0' },
    { count: Number.POSITIVE_INFINITY, text: '0' },
  ])('should show $count tokens as $text', ({ count, text }) => {
    expect(formatTokens(count)).toBe(text)
  })
})
