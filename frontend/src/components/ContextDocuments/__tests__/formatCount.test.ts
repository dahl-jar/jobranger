import { describe, expect, it } from 'vitest'
import { formatCount } from '@/components/ContextDocuments/formatCount'
import { en } from '@/i18n/en'
import { nb } from '@/i18n/nb'

const NO_BREAK_SPACE = '\u00a0'

describe('formatCount', () => {
  it.each([
    { text: en, count: 12_345, shown: '12,345' },
    { text: nb, count: 12_345, shown: `12${NO_BREAK_SPACE}345` },
  ])('should show $shown', ({ text, count, shown }) => {
    expect(formatCount(count, text)).toBe(shown)
  })
})
