import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { THEME_STORAGE_KEY } from '@/app/theme/theme'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import { AppearanceSettings } from '@/features/settings/AppearanceSettings'

function renderAppearance() {
  render(
    <ThemeProvider>
      <AppearanceSettings />
    </ThemeProvider>,
  )
}

describe('AppearanceSettings', () => {
  it.each([
    { theme: 'Light', hint: 'Warm paper' },
    { theme: 'System', hint: 'Follows your computer' },
    { theme: 'Dark', hint: 'Default' },
  ])('should describe $theme as $hint', ({ theme, hint }) => {
    renderAppearance()

    expect(screen.getByRole('radio', { name: theme })).toHaveAccessibleDescription(hint)
  })

  it('should check Dark when this browser has no theme saved', () => {
    renderAppearance()

    expect(screen.getByRole('radio', { name: 'Dark' })).toBeChecked()
  })

  it('should check the theme this browser uses', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'system')

    renderAppearance()

    expect(screen.getByRole('radio', { name: 'System' })).toBeChecked()
  })

  it('should save the picked theme in this browser', async () => {
    const user = userEvent.setup()
    renderAppearance()

    await user.click(screen.getByRole('radio', { name: 'Light' }))

    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
    expect(screen.getByRole('radio', { name: 'Light' })).toBeChecked()
  })
})
