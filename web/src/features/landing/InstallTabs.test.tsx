import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { InstallTabs } from '@/features/landing/InstallTabs'

const COPY_RESET_MS = 2000

async function copyWithFakeClock() {
  userEvent.setup()
  vi.useFakeTimers()
  onTestFinished(() => {
    vi.useRealTimers()
  })
  render(<InstallTabs />)
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name: 'Copy' }))
  })
}

describe('InstallTabs', () => {
  it('should show only the pip and uv tabs', () => {
    render(<InstallTabs />)

    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['pip', 'uv'])
  })

  it('should show the uv command when the uv tab is picked', async () => {
    const user = userEvent.setup()
    render(<InstallTabs />)

    await user.click(screen.getByRole('tab', { name: 'uv' }))

    expect(screen.getByRole('tab', { name: 'uv' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'pip' })).toHaveAttribute('aria-selected', 'false')
    expect(screen.getByRole('tabpanel')).toHaveTextContent('uv tool install jobranger')
  })

  it('should point each tab at its own panel', async () => {
    const user = userEvent.setup()
    render(<InstallTabs />)

    await user.click(screen.getByRole('tab', { name: 'uv' }))

    expect(document.getElementById(screen.getByRole('tab', { name: 'uv' }).getAttribute('aria-controls') ?? '')).toBe(screen.getByRole('tabpanel'))
  })

  it('should keep only the picked tab in the tab order', async () => {
    const user = userEvent.setup()
    render(<InstallTabs />)

    await user.click(screen.getByRole('tab', { name: 'uv' }))

    expect(screen.getAllByRole('tab').map((tab) => tab.tabIndex)).toEqual([-1, 0])
  })

  it('should keep End from scrolling the page', () => {
    render(<InstallTabs />)

    const notPrevented = fireEvent.keyDown(screen.getByRole('tab', { name: 'pip' }), { key: 'End' })

    expect(notPrevented).toBe(false)
  })

  it.each([
    { name: 'Right arrow', key: '{ArrowRight}', from: 'uv', to: 'pip' },
    { name: 'Left arrow', key: '{ArrowLeft}', from: 'pip', to: 'uv' },
    { name: 'Home', key: '{Home}', from: 'uv', to: 'pip' },
    { name: 'Home', key: '{Home}', from: 'pip', to: 'pip' },
    { name: 'End', key: '{End}', from: 'pip', to: 'uv' },
    { name: 'End', key: '{End}', from: 'uv', to: 'uv' },
  ])('should move to the $to tab on $name from $from', async ({ key, from, to }) => {
    const user = userEvent.setup()
    render(<InstallTabs />)
    await user.click(screen.getByRole('tab', { name: from }))

    await user.keyboard(key)

    expect(screen.getByRole('tab', { name: to })).toHaveFocus()
    expect(screen.getByRole('tab', { name: to })).toHaveAttribute('aria-selected', 'true')
  })

  it('should leave the tab alone on other keys', async () => {
    const user = userEvent.setup()
    render(<InstallTabs />)
    await user.click(screen.getByRole('tab', { name: 'uv' }))

    await user.keyboard('{ArrowDown}')

    expect(screen.getByRole('tab', { name: 'uv' })).toHaveAttribute('aria-selected', 'true')
  })

  it('should copy the shown command', async () => {
    const user = userEvent.setup()
    render(<InstallTabs />)

    await user.click(screen.getByRole('button', { name: 'Copy' }))

    expect(await navigator.clipboard.readText()).toBe('pip install jobranger')
    expect(screen.getByRole('button', { name: 'Copied' })).toBeInTheDocument()
  })

  it('should say Copy failed when the clipboard refuses', async () => {
    const user = userEvent.setup()
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new DOMException('denied', 'NotAllowedError'))
    render(<InstallTabs />)

    await user.click(screen.getByRole('button', { name: 'Copy' }))

    expect(screen.getByRole('button', { name: 'Copy failed' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Copy failed')
  })

  it('should go back to Copy two seconds after copying', async () => {
    await copyWithFakeClock()

    act(() => {
      vi.advanceTimersByTime(COPY_RESET_MS)
    })

    expect(screen.getByRole('button', { name: 'Copy' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })

  it('should keep Copy failed for two seconds after an earlier copy', async () => {
    await copyWithFakeClock()
    act(() => {
      vi.advanceTimersByTime(COPY_RESET_MS / 2)
    })
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new DOMException('denied', 'NotAllowedError'))
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Copied' }))
    })

    act(() => {
      vi.advanceTimersByTime(COPY_RESET_MS / 2 + 1)
    })

    expect(screen.getByRole('button', { name: 'Copy failed' })).toBeInTheDocument()
  })

  it('should keep Copied just under two seconds after copying', async () => {
    await copyWithFakeClock()

    act(() => {
      vi.advanceTimersByTime(COPY_RESET_MS - 1)
    })

    expect(screen.getByRole('button', { name: 'Copied' })).toBeInTheDocument()
  })
})
