import { describe, expect, it } from 'vitest'
import { THEME_STORAGE_KEY } from '@/helpers/applyTheme'

describe('themeInit', () => {
  it('should paint the stored theme when the script loads', async () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')

    await import('@/helpers/paintThemeOnLoad')

    expect(document.documentElement.dataset.theme).toBe('light')
  })
})
