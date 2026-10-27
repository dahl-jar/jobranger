import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { LANGUAGE_STORAGE_KEY } from '@/shared/i18n/language'
import { useLanguage, useText } from '@/shared/i18n/languageContext'
import { LanguageProvider } from '@/shared/i18n/LanguageProvider'

function Probe() {
  const text = useText()
  const { language, setLanguage } = useLanguage()
  return (
    <>
      <p>{text.app.notFound.heading}</p>
      <button type="button" onClick={() => setLanguage(language === 'en' ? 'nb' : 'en')}>
        switch
      </button>
    </>
  )
}

function renderProbe() {
  render(
    <LanguageProvider>
      <Probe />
    </LanguageProvider>,
  )
}

describe('LanguageProvider', () => {
  it('should show Norwegian text when the browser asks for Norwegian', () => {
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('nb-NO')

    renderProbe()

    expect(screen.getByText('Ingen side her.')).toBeInTheDocument()
  })

  it('should mark the page as nb when nb is saved', () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'nb')

    renderProbe()

    expect(document.documentElement.lang).toBe('nb')
  })

  it('should save the new language', async () => {
    const user = userEvent.setup()
    renderProbe()

    await user.click(screen.getByRole('button', { name: 'switch' }))

    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('nb')
  })

  it('should follow a language picked in another tab', () => {
    renderProbe()
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'nb')

    act(() => {
      window.dispatchEvent(new StorageEvent('storage', { key: LANGUAGE_STORAGE_KEY, newValue: 'nb' }))
    })

    expect(screen.getByText('Ingen side her.')).toBeInTheDocument()
    expect(document.documentElement.lang).toBe('nb')
  })

  it('should fall back to the browser language when another tab clears storage', () => {
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('en-US')
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'nb')
    renderProbe()
    localStorage.clear()

    act(() => {
      window.dispatchEvent(new StorageEvent('storage', { key: null }))
    })

    expect(screen.getByText('No page here.')).toBeInTheDocument()
    expect(document.documentElement.lang).toBe('en')
  })

  it('should show English without a provider above it', () => {
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('nb-NO')

    render(<Probe />)

    expect(screen.getByText('No page here.')).toBeInTheDocument()
  })
})
