import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { StatusMenu } from '@/features/applications/StatusMenu'
import styles from '@/features/applications/StatusMenu.module.css'

function renderMenu(disabled = false) {
  const onChange = vi.fn()
  render(
    <>
      <StatusMenu status="interview" onChange={onChange} disabled={disabled} />
      <button type="button">Outside</button>
    </>,
  )
  return { onChange, button: screen.getByRole('button', { name: 'Status: Interview' }) }
}

describe('StatusMenu', () => {
  it('should focus the checked item when opened', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()

    await user.click(button)

    expect(button).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('menuitemradio', { name: 'Interview' })).toHaveFocus()
  })

  it('should return focus to the button after choosing with Enter', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()
    await user.click(button)

    await user.keyboard('{End}{Enter}')

    expect(button).toHaveFocus()
  })

  it('should keep the page from scrolling on arrow keys', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()
    await user.click(button)

    const notPrevented = fireEvent.keyDown(screen.getByRole('menuitemradio', { name: 'Interview' }), { key: 'ArrowDown' })

    expect(notPrevented).toBe(false)
  })

  it('should choose a status with a click', async () => {
    const user = userEvent.setup()
    const { onChange, button } = renderMenu()
    await user.click(button)

    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))

    expect(onChange).toHaveBeenCalledExactlyOnceWith('offer')
  })

  it('should not send the status it already has', async () => {
    const user = userEvent.setup()
    const { onChange, button } = renderMenu()
    await user.click(button)

    await user.keyboard('{Enter}')

    expect(onChange).not.toHaveBeenCalled()
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it.each(['{Escape}', '{Tab}'])('should close on %s without sending a command', async (key) => {
    const user = userEvent.setup()
    const { onChange, button } = renderMenu()
    await user.click(button)

    await user.keyboard(key)

    expect(onChange).not.toHaveBeenCalled()
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(button).toHaveAttribute('aria-expanded', 'false')
  })

  it('should return focus to the button on Escape', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()
    await user.click(button)

    await user.keyboard('{Escape}')

    expect(button).toHaveFocus()
  })

  it('should close when the pointer goes down outside', async () => {
    const user = userEvent.setup()
    const { onChange, button } = renderMenu()
    await user.click(button)

    fireEvent.pointerDown(screen.getByRole('button', { name: 'Outside' }))

    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()
  })

  it('should stay open when the pointer goes down on an item', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()
    await user.click(button)

    fireEvent.pointerDown(screen.getByRole('menuitemradio', { name: 'Offer' }))

    expect(screen.getByRole('menu')).toBeInTheDocument()
  })

  it('should close when the button is clicked again', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()
    await user.click(button)

    await user.click(button)

    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('should not open while edits are off', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu(true)

    await user.click(button)

    expect(button).toBeDisabled()
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('should move down from the checked item', async () => {
    const user = userEvent.setup()
    const { onChange, button } = renderMenu()
    await user.click(button)

    await user.keyboard('{ArrowDown}{ArrowDown}{Enter}')

    expect(onChange).toHaveBeenCalledExactlyOnceWith('accepted')
  })

  it('should check only the current status', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()

    await user.click(button)

    expect(screen.getAllByRole('menuitemradio').map((item) => item.getAttribute('aria-checked'))).toEqual(['false', 'true', 'false', 'false', 'false'])
  })

  it('should keep Escape from reaching the dialog around it', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()
    await user.click(button)

    const notPrevented = fireEvent.keyDown(screen.getByRole('menuitemradio', { name: 'Interview' }), { key: 'Escape' })

    expect(notPrevented).toBe(false)
  })

  it('should keep Escape inside the menu', async () => {
    const user = userEvent.setup()
    const onOuterKeyDown = vi.fn()
    render(
      <div onKeyDown={onOuterKeyDown}>
        <StatusMenu status="waiting" onChange={vi.fn()} disabled={false} />
      </div>,
    )
    await user.click(screen.getByRole('button', { name: 'Status: Waiting' }))

    await user.keyboard('{Escape}')

    expect(onOuterKeyDown).not.toHaveBeenCalled()
  })

  it('should keep the items out of the Tab order', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()

    await user.click(button)

    expect(screen.getAllByRole('menuitemradio').map((item) => item.tabIndex)).toEqual([-1, -1, -1, -1, -1])
  })

  it('should class a closed button without the open class', () => {
    render(<StatusMenu status="waiting" onChange={vi.fn()} disabled={false} />)

    expect(screen.getByRole('button', { name: 'Status: Waiting' }).className).toBe(`${styles.button} ${styles.waiting}`)
  })

  it('should class an open button as open', async () => {
    const user = userEvent.setup()
    const { button } = renderMenu()

    await user.click(button)

    expect(button.className).toBe(`${styles.button} ${styles.interview} ${styles.open}`)
  })
})
