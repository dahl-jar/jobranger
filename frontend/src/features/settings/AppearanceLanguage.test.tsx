import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import type { ConfigPatch } from '@/features/relay/contract/payload.gen'
import { AppearanceSettings } from '@/features/settings/AppearanceSettings'
import { LANGUAGE_STORAGE_KEY } from '@/shared/i18n/language'
import { LanguageProvider } from '@/shared/i18n/LanguageProvider'

function renderAppearance(onPatch?: (patch: ConfigPatch) => void) {
  render(
    <LanguageProvider>
      <ThemeProvider>
        <AppearanceSettings {...(onPatch === undefined ? {} : { onPatch })} />
      </ThemeProvider>
    </LanguageProvider>,
  )
}

describe('AppearanceSettings language', () => {
  it('should check English when no language provider is above it', () => {
    render(
      <ThemeProvider>
        <AppearanceSettings />
      </ThemeProvider>,
    )

    expect(screen.getByRole('radio', { name: 'English' })).toBeChecked()
  })

  it.each([
    { name: 'English', lang: 'en' },
    { name: 'Norsk bokmål', lang: 'nb' },
  ])('should mark $name as written in $lang', ({ name, lang }) => {
    renderAppearance()

    expect(screen.getByText(name)).toHaveAttribute('lang', lang)
  })

  it('should check the language the page uses', () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'nb')

    renderAppearance()

    expect(screen.getByRole('radio', { name: 'Norsk bokmål' })).toBeChecked()
  })

  it('should switch the page to bokmål when Norsk bokmål is picked', async () => {
    const user = userEvent.setup()
    renderAppearance()

    await user.click(screen.getByRole('radio', { name: 'Norsk bokmål' }))

    expect(screen.getByRole('group', { name: 'Språk' })).toBeInTheDocument()
  })

  it('should send the picked language to the paired machine', async () => {
    const user = userEvent.setup()
    const onPatch = vi.fn()
    renderAppearance(onPatch)

    await user.click(screen.getByRole('radio', { name: 'Norsk bokmål' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ language: 'nb' })
  })
})
