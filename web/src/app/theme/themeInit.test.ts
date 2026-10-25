import { describe, expect, it } from 'vitest'
import { THEME_STORAGE_KEY } from '@/app/theme/theme'

describe('themeInit', () => {
  it('should paint the stored theme when the script loads', async () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')

    await import('@/app/theme/themeInit')

    expect(document.documentElement.dataset.theme).toBe('light')
  })
})
