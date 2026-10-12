import { describe, expect, it, vi } from 'vitest'
import { readStoredTheme, resolveTheme, saveTheme, THEME_STORAGE_KEY } from '@/app/theme/theme'

describe('resolveTheme', () => {
  it.each([
    { choice: 'system', prefersLight: true, expected: 'light' },
    { choice: 'system', prefersLight: false, expected: 'dark' },
    { choice: 'dark', prefersLight: true, expected: 'dark' },
  ] as const)('should resolve $choice to $expected when prefers light is $prefersLight', ({ choice, prefersLight, expected }) => {
    expect(resolveTheme(choice, prefersLight)).toBe(expected)
  })
})

describe('readStoredTheme', () => {
  it('should default to dark when storage is empty', () => {
    expect(readStoredTheme()).toBe('dark')
  })

  it('should default to dark when the stored value is invalid', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'sepia')

    expect(readStoredTheme()).toBe('dark')
  })

  it('should return the stored choice', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')

    expect(readStoredTheme()).toBe('light')
  })

  it('should default to dark when storage is blocked', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError')
    })

    expect(readStoredTheme()).toBe('dark')
  })
})

describe('saveTheme', () => {
  it('should store the choice under jobranger.theme', () => {
    saveTheme('light')

    expect(localStorage.getItem('jobranger.theme')).toBe('light')
  })
})
