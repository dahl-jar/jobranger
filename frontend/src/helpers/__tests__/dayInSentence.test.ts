import { describe, expect, it } from 'vitest'
import { dayInSentence } from '@/helpers/dayInSentence'
import { TODAY } from '@/__tests__/testClock'

describe('dayInSentence', () => {
  it.each([
    ['today', '2026-10-12T12:00:00'],
    ['yesterday', '2026-10-11T12:00:00'],
  ])('should write %s in lowercase', (text, iso) => {
    expect(dayInSentence(iso, TODAY)).toBe(text)
  })

  it('should keep an older day as it is', () => {
    expect(dayInSentence('2026-10-10T12:00:00', TODAY)).toBe('10 Oct')
  })
})
