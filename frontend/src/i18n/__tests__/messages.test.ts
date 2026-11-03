import { describe, expect, expectTypeOf, it } from 'vitest'
import type { Language as ContractLanguage } from '@/contract/payload.gen'
import { en } from '@/i18n/en'
import type { Language } from '@/helpers/languageChoice'
import { GLOSSARY, nb } from '@/i18n/nb'
import { tableShape, tableTexts } from '@/__tests__/findEnglishText'

describe('message tables', () => {
  it('should have a table for English and bokmål only', () => {
    expectTypeOf<Language>().toEqualTypeOf<ContractLanguage>()
  })

  it('should give both languages the same keys', () => {
    expect(tableShape(nb)).toEqual(tableShape(en))
  })

  it.each(Object.entries(GLOSSARY))('should not use a rejected word for %s', (_, term) => {
    const rejected = new RegExp(`(?<!\\p{L})(${term.rejected.join('|')})(?!\\p{L})`, 'iu')

    expect(tableTexts(nb).filter((text) => rejected.test(text))).toEqual([])
  })

  it.each(Object.entries(GLOSSARY))('should use the chosen word for %s', (_, term) => {
    const chosen = new RegExp(`(?<!\\p{L})${term.nb}`, 'iu')

    expect(tableTexts(nb).some((text) => chosen.test(text))).toBe(true)
  })
})
