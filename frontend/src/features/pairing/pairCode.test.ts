import { describe, expect, it } from 'vitest'
import { formatPairCode, normalizePairCode, validatePairCode } from '@/features/pairing/pairCode'

describe('normalizePairCode', () => {
  it.each(['kqfb mxtw', 'kqfb-mxtw', ' KQFB-MXTW '])('should normalize "%s" to KQFBMXTW', (raw) => {
    expect(normalizePairCode(raw)).toBe('KQFBMXTW')
  })
})

describe('validatePairCode', () => {
  it('should accept eight letters from the code alphabet', () => {
    expect(validatePairCode('KQFBMXTW')).toBeUndefined()
  })

  it('should call an empty code empty', () => {
    expect(validatePairCode('')).toBe('empty')
  })

  it.each([
    { rule: 'a vowel', code: 'KQFAMXTW' },
    { rule: 'a digit', code: 'KQF2MXTW' },
    { rule: 'seven letters', code: 'KQFBMXT' },
    { rule: 'nine letters', code: 'KQFBMXTWZ' },
    { rule: 'a symbol', code: 'KQFB.MXT' },
  ])('should call a code with $rule invalid', ({ code }) => {
    expect(validatePairCode(code)).toBe('invalid')
  })
})

describe('formatPairCode', () => {
  it('should put a hyphen after four letters', () => {
    expect(formatPairCode('KQFBMXTW')).toBe('KQFB-MXTW')
  })
})
