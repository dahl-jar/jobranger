import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AddApplicationForm } from '@/features/applications/AddApplicationForm'
import { COMPANY_MAX_CHARS, NOTE_MAX_CHARS, TITLE_MAX_CHARS } from '@/features/applications/limits'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'

const created = payloadFixtures['application.create']

function renderForm(onAdd = vi.fn().mockResolvedValue(undefined), disabled = false) {
  render(<AddApplicationForm onAdd={onAdd} onCancel={vi.fn()} disabled={disabled} />)
  return onAdd
}

describe('AddApplicationForm', () => {
  it('should block a company of only spaces', async () => {
    const user = userEvent.setup()
    const onAdd = renderForm()
    await user.type(screen.getByLabelText('Company'), '   ')

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(onAdd).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Enter a company.')
    expect(screen.getByLabelText('Company')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Company')).toHaveFocus()
  })

  it('should send company, role and note trimmed', async () => {
    const user = userEvent.setup()
    const onAdd = renderForm()
    await user.type(screen.getByLabelText('Company'), ` ${created.company} `)
    await user.type(screen.getByLabelText('Role'), `${created.title} `)
    await user.type(screen.getByLabelText('Note'), ` ${created.note}`)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(onAdd).toHaveBeenCalledExactlyOnceWith({ company: created.company, title: created.title, note: created.note })
  })

  it('should clear the company error once a company is sent', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.click(screen.getByRole('button', { name: 'Add' }))
    await user.type(screen.getByLabelText('Company'), created.company)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Company')).toHaveAttribute('aria-invalid', 'false')
  })

  it('should send once while the machine has not answered', async () => {
    const user = userEvent.setup()
    const onAdd = renderForm(vi.fn().mockReturnValue(new Promise<void>(() => undefined)))
    await user.type(screen.getByLabelText('Company'), created.company)

    await user.click(screen.getByRole('button', { name: 'Add' }))
    await user.click(screen.getByRole('button', { name: 'Adding...' }))

    expect(onAdd).toHaveBeenCalledOnce()
  })

  it('should show the refusal when the machine refuses', async () => {
    const user = userEvent.setup()
    renderForm(vi.fn().mockRejectedValue(new Error('no answer from your machine')))
    await user.type(screen.getByLabelText('Company'), created.company)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Not saved: no answer from your machine')
  })

  it('should keep the draft when the machine refuses', async () => {
    const user = userEvent.setup()
    renderForm(vi.fn().mockRejectedValue(new Error('no answer from your machine')))
    await user.type(screen.getByLabelText('Company'), created.company)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('button', { name: 'Add' })).toBeEnabled()
    expect(screen.getByLabelText('Company')).toHaveValue(created.company)
  })

  it('should not send while edits are off', () => {
    renderForm(vi.fn(), true)

    expect(screen.getByRole('button', { name: 'Add' })).toBeDisabled()
  })

  it('should keep the page from reloading on submit', () => {
    renderForm()

    const notPrevented = fireEvent.submit(screen.getByRole('form', { name: 'Add an application' }))

    expect(notPrevented).toBe(false)
  })

  it('should describe the company field with its error', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.getByLabelText('Company')).toHaveAccessibleDescription('Enter a company.')
  })

  it('should clear the fields after the machine saves', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.type(screen.getByLabelText('Company'), created.company)
    await user.type(screen.getByLabelText('Note'), created.note)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.getByLabelText('Company')).toHaveValue('')
    expect(screen.getByLabelText('Note')).toHaveValue('')
  })

  it('should focus Company after the machine saves', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.type(screen.getByLabelText('Company'), created.company)
    await user.click(screen.getByLabelText('Note'))

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.getByLabelText('Company')).toHaveFocus()
  })

  it('should enable Add again after the machine saves', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.type(screen.getByLabelText('Company'), created.company)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.getByRole('button', { name: 'Add' })).toBeEnabled()
  })

  it.each([
    { field: 'Company', max: COMPANY_MAX_CHARS, message: `Company stops at ${COMPANY_MAX_CHARS} characters.` },
    { field: 'Role', max: TITLE_MAX_CHARS, message: `Role stops at ${TITLE_MAX_CHARS} characters.` },
    { field: 'Note', max: NOTE_MAX_CHARS, message: `Note stops at ${NOTE_MAX_CHARS} characters.` },
  ])('should cap $field at the contract limit', ({ field, max }) => {
    renderForm()

    fireEvent.change(screen.getByLabelText(field), { target: { value: 'x'.repeat(max + 1) } })

    expect(screen.getByLabelText(field)).toHaveValue('x'.repeat(max))
    expect(screen.getByLabelText(field)).toHaveAttribute('maxlength', String(max))
  })

  it.each([
    { field: 'Company', max: COMPANY_MAX_CHARS },
    { field: 'Role', max: TITLE_MAX_CHARS },
    { field: 'Note', max: NOTE_MAX_CHARS },
  ])('should describe $field by its limit when full', ({ field, max }) => {
    renderForm()

    fireEvent.change(screen.getByLabelText(field), { target: { value: 'x'.repeat(max) } })

    expect(screen.getByLabelText(field)).toHaveAccessibleDescription(`${field} stops at ${max} characters.`)
  })

  it('should focus Company when the form opens', () => {
    renderForm()

    expect(screen.getByLabelText('Company')).toHaveFocus()
  })

  it('should leave Company undescribed without an error or limit', () => {
    renderForm()

    expect(screen.getByLabelText('Company')).not.toHaveAttribute('aria-describedby')
  })

  it('should describe Role only by its own limit when the company is missing', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.click(screen.getByRole('button', { name: 'Add' }))

    fireEvent.change(screen.getByLabelText('Role'), { target: { value: 'x'.repeat(TITLE_MAX_CHARS) } })

    expect(screen.getByLabelText('Role')).toHaveAccessibleDescription(`Role stops at ${TITLE_MAX_CHARS} characters.`)
  })

  it('should describe Company by both messages when missing at its limit', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.click(screen.getByRole('button', { name: 'Add' }))

    fireEvent.change(screen.getByLabelText('Company'), { target: { value: 'x'.repeat(COMPANY_MAX_CHARS) } })

    expect(screen.getByLabelText('Company')).toHaveAccessibleDescription(`Enter a company. Company stops at ${COMPANY_MAX_CHARS} characters.`)
  })
})
