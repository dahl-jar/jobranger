import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { ReplaceKeyForm } from '@/features/settings/ReplaceKeyForm'

const KEY = payloadFixtures['key.replace'].key
const SPACED_KEY = 'test key 0000'

function renderForm() {
  const onSave = vi.fn()
  const onCancel = vi.fn()
  render(<ReplaceKeyForm focusOnOpen={false} onSave={onSave} onCancel={onCancel} />)
  return { onSave, onCancel }
}

describe('ReplaceKeyForm', () => {
  it('should send the key', async () => {
    const user = userEvent.setup()
    const { onSave } = renderForm()
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(onSave).toHaveBeenCalledExactlyOnceWith(KEY)
  })

  it('should clear the field after the key is sent', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(screen.getByLabelText('New API key')).toHaveValue('')
  })

  it('should keep the typed key out of the page markup', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText('New API key'), KEY)

    expect(document.body.innerHTML).not.toContain(KEY)
    expect(screen.getByLabelText('New API key')).toHaveAttribute('autocomplete', 'new-password')
  })

  it('should drop spaces around a pasted key', async () => {
    const user = userEvent.setup()
    const { onSave } = renderForm()
    await user.type(screen.getByLabelText('New API key'), `  ${KEY} `)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(onSave).toHaveBeenCalledWith(KEY)
  })

  it('should refuse a key with a space', async () => {
    const user = userEvent.setup()
    const { onSave } = renderForm()
    await user.type(screen.getByLabelText('New API key'), SPACED_KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(onSave).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('An API key has 12 or more characters and no spaces.')
  })

  it('should keep a refused key in the field', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.type(screen.getByLabelText('New API key'), SPACED_KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(screen.getByLabelText('New API key')).toHaveValue(SPACED_KEY)
  })

  it('should drop the key error after a good key', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.type(screen.getByLabelText('New API key'), 'short-key')
    await user.click(screen.getByRole('button', { name: 'Save key' }))
    await user.clear(screen.getByLabelText('New API key'))
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: 'Save key' }))

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should keep the page in place when the key form is sent', () => {
    renderForm()

    const notPrevented = fireEvent.submit(screen.getByRole('button', { name: 'Save key' }))

    expect(notPrevented).toBe(false)
  })

  it('should show no error before a key is sent', () => {
    renderForm()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should close without sending on Cancel', async () => {
    const user = userEvent.setup()
    const { onSave, onCancel } = renderForm()
    await user.type(screen.getByLabelText('New API key'), KEY)

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalledOnce()
    expect(onSave).not.toHaveBeenCalled()
  })
})
