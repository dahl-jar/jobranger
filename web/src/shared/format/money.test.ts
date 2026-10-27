import { describe, expect, it } from 'vitest'
import { formatUsd } from '@/shared/format/money'

describe('formatUsd', () => {
  it.each([
    { value: 0, text: '$0.00' },
    { value: 5, text: '$5.00' },
    { value: 1234.56, text: '$1,234.56' },
    { value: 0.004, text: '<$0.01' },
    { value: 0.01, text: '$0.01' },
    { value: -5, text: '-$5.00' },
    { value: Number.NaN, text: '$0.00' },
    { value: Number.POSITIVE_INFINITY, text: '$0.00' },
  ])('should show $value as $text', ({ value, text }) => {
    expect(formatUsd(value)).toBe(text)
  })
})
