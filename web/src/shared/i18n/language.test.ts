import { describe, expect, it, vi } from 'vitest'
import { initialLanguage, LANGUAGE_STORAGE_KEY, saveLanguage } from '@/shared/i18n/language'
import { blockStorage } from '@/test/stubs'

function stubBrowserLanguage(locale: string) {
  vi.spyOn(navigator, 'language', 'get').mockReturnValue(locale)
}

describe('initialLanguage', () => {
  it.each(['nb', 'nb-NO', 'no', 'nn', 'NB-no'])('should pick bokmål when the browser asks for "%s"', (locale) => {
    stubBrowserLanguage(locale)

    expect(initialLanguage()).toBe('nb')
  })

  it.each(['en-US', '', 'nob'])('should pick English when the browser asks for "%s"', (locale) => {
    stubBrowserLanguage(locale)

    expect(initialLanguage()).toBe('en')
  })

  it('should prefer the saved language over the browser language', () => {
    stubBrowserLanguage('nb-NO')
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en')

    expect(initialLanguage()).toBe('en')
  })

  it('should ignore a saved language it does not know', () => {
    stubBrowserLanguage('nb-NO')
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'de')

    expect(initialLanguage()).toBe('nb')
  })

  it('should follow the browser language when storage is blocked', () => {
    stubBrowserLanguage('nb-NO')
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('The operation is insecure.', 'SecurityError')
    })

    expect(initialLanguage()).toBe('nb')
  })
})

describe('saveLanguage', () => {
  it('should keep the page working when storage is blocked', () => {
    blockStorage('setItem')

    expect(() => saveLanguage('nb')).not.toThrow()
  })
})
