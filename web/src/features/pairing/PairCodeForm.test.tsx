import { createEvent, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { PairCodeForm } from '@/features/pairing/PairCodeForm'
import type { EndReason, FormError } from '@/features/pairing/pairingMachine'

function renderForm({ error, ended }: { error?: FormError; ended?: EndReason } = {}) {
  render(
    <MemoryRouter>
      <PairCodeForm error={error} ended={ended} busy={false} onSubmit={() => undefined} />
    </MemoryRouter>,
  )
  return screen.getByLabelText('Enter the code it shows')
}

describe('PairCodeForm', () => {
  it('should describe the code field with the error and the help line', () => {
    const input = renderForm({ error: 'wrong' })

    expect(input).toHaveAccessibleDescription("That code doesn't match. Check your terminal. 8 letters, valid for 10 minutes.")
  })

  it('should keep the page from reloading on submit', () => {
    const form = renderForm().closest('form')
    const submit = createEvent.submit(form ?? document.body)

    fireEvent(form ?? document.body, submit)

    expect(submit.defaultPrevented).toBe(true)
  })

  it('should say the terminal stopped waiting after the word compare timed out', () => {
    renderForm({ ended: 'timeout' })

    expect(screen.getByRole('alert')).toHaveTextContent('Your terminal stopped waiting. Run jobranger pair again.')
  })

  it('should list where the data stays', () => {
    renderForm()

    expect(screen.getAllByRole('listitem').map((item) => item.textContent).slice(-3)).toEqual([
      'Your data stays on your machine.',
      'This page sees only what your machine sends while open.',
      'Unpair with jobranger unpair.',
    ])
  })

  it('should leave focus alone when the form opens without an error', () => {
    const input = renderForm()

    expect(input).not.toHaveFocus()
  })

  it('should mark the code valid before an error', () => {
    const input = renderForm()

    expect(input).toHaveAttribute('aria-invalid', 'false')
  })

  it('should turn spell checking off for the code', () => {
    const input = renderForm()

    expect(input).toHaveAttribute('spellcheck', 'false')
  })
})
