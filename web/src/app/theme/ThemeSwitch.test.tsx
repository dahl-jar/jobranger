import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { THEME_STORAGE_KEY } from '@/app/theme/theme'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import { ThemeSwitch } from '@/app/theme/ThemeSwitch'
import styles from '@/app/theme/ThemeSwitch.module.css'
import { Icon } from '@/shared/ui/Icon'
import { setPrefersLight } from '@/test/matchMedia'
import { blockStorage } from '@/test/stubs'

function renderSwitch(variant: 'icons' | 'labeled' = 'icons') {
  return render(
    <ThemeProvider>
      <ThemeSwitch variant={variant} />
    </ThemeProvider>,
  )
}

describe('ThemeSwitch', () => {
  it('should paint the stored theme when the page opens', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')

    renderSwitch()

    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('should follow a theme picked in another tab', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')
    renderSwitch()
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')

    act(() => {
      window.dispatchEvent(new StorageEvent('storage', { key: THEME_STORAGE_KEY, newValue: 'dark' }))
    })

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(screen.getByRole('button', { name: 'Dark theme' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('should switch the page to light when Light theme is pressed', async () => {
    const user = userEvent.setup()
    renderSwitch()

    await user.click(screen.getByRole('button', { name: 'Light theme' }))

    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('should keep System picked after a reload', async () => {
    const user = userEvent.setup()
    const { unmount } = renderSwitch()
    await user.click(screen.getByRole('button', { name: 'System theme' }))
    unmount()

    renderSwitch()

    expect(screen.getByRole('button', { name: 'System theme' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('should mark System pressed when system is picked on a dark computer', async () => {
    const user = userEvent.setup()
    renderSwitch()

    await user.click(screen.getByRole('button', { name: 'System theme' }))

    expect(screen.getByRole('button', { name: 'System theme' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Dark theme' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('should follow the computer switching to light while System is picked', async () => {
    const user = userEvent.setup()
    renderSwitch()
    await user.click(screen.getByRole('button', { name: 'System theme' }))

    act(() => setPrefersLight(true))

    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('should stop following the computer after Dark is picked', async () => {
    const user = userEvent.setup()
    renderSwitch()
    await user.click(screen.getByRole('button', { name: 'System theme' }))
    await user.click(screen.getByRole('button', { name: 'Dark theme' }))

    act(() => setPrefersLight(true))

    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  it('should show the theme names as text in the labeled switch', () => {
    renderSwitch('labeled')

    expect(screen.getByRole('button', { name: 'Light' })).toBeInTheDocument()
  })

  it('should show only icons in the icons switch', () => {
    renderSwitch('icons')
    const button = screen.getByRole('button', { name: 'Light theme' })

    expect(button.textContent).toBe('')
    expect(button.querySelector('svg')).toBeInTheDocument()
  })

  it.each([
    { name: 'Light theme', icon: 'sun' },
    { name: 'System theme', icon: 'monitor' },
    { name: 'Dark theme', icon: 'moon' },
  ] as const)('should draw the $icon icon on $name', ({ name, icon }) => {
    const { container } = render(<Icon name={icon} />)
    renderSwitch('icons')

    expect(screen.getByRole('button', { name }).innerHTML).toBe(container.innerHTML)
  })

  it('should style the icon buttons as icons', () => {
    renderSwitch('icons')

    expect([...screen.getByRole('button', { name: 'Light theme' }).classList]).toEqual([styles.option, styles.icon])
  })

  it('should still switch the theme when storage is blocked', async () => {
    const user = userEvent.setup()
    blockStorage('setItem')
    renderSwitch()

    await user.click(screen.getByRole('button', { name: 'Light theme' }))

    expect(screen.getByRole('button', { name: 'Light theme' })).toHaveAttribute('aria-pressed', 'true')
    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('should throw a named error when there is no ThemeProvider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    expect(() => render(<ThemeSwitch variant="icons" />)).toThrow('useTheme needs a ThemeProvider above it')
  })
})
