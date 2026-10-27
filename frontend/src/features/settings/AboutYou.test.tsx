import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { ContextSnapshot, ErrorMessage } from '@/features/relay/contract/payload.gen'
import { AboutYou } from '@/features/settings/AboutYou'

const CONTEXT = payloadFixtures['context.snapshot']

function renderAbout(context: ContextSnapshot = CONTEXT, errorCode?: ErrorMessage['code']) {
  const onSave = vi.fn()
  render(<AboutYou context={context} errorCode={errorCode} onSave={onSave} />)
  return { onSave, user: userEvent.setup() }
}

describe('AboutYou', () => {
  it('should send the changed text when the field loses focus', async () => {
    const { onSave, user } = renderAbout()
    await user.type(screen.getByLabelText('About you'), ' Built a booking API.  ')

    await user.tab()

    expect(onSave).toHaveBeenCalledExactlyOnceWith(`${CONTEXT.about} Built a booking API.`)
  })

  it('should not send when the text did not change', async () => {
    const { onSave, user } = renderAbout()
    await user.click(screen.getByLabelText('About you'))

    await user.tab()

    expect(onSave).not.toHaveBeenCalled()
  })

  it('should send text at exactly the limit', async () => {
    const { onSave, user } = renderAbout({ ...CONTEXT, about_limit: CONTEXT.about.length + 1 })
    await user.type(screen.getByLabelText('About you'), '!')

    await user.tab()

    expect(onSave).toHaveBeenCalledExactlyOnceWith(`${CONTEXT.about}!`)
  })

  it('should not send text over the limit', async () => {
    const { onSave, user } = renderAbout({ ...CONTEXT, about_limit: CONTEXT.about.length })
    await user.click(screen.getByLabelText('About you'))
    fireEvent.change(screen.getByLabelText('About you'), { target: { value: `${CONTEXT.about}!` } })

    await user.tab()

    expect(onSave).not.toHaveBeenCalled()
    expect(screen.getByText(`${CONTEXT.about.length + 1} / ${CONTEXT.about.length}`)).toBeInTheDocument()
  })

  it('should show new text from the machine', () => {
    const { rerender } = render(<AboutYou context={CONTEXT} errorCode={undefined} onSave={vi.fn()} />)

    rerender(<AboutYou context={{ ...CONTEXT, about: 'Moving to Oslo.' }} errorCode={undefined} onSave={vi.fn()} />)

    expect(screen.getByLabelText('About you')).toHaveValue('Moving to Oslo.')
  })

  it('should count the characters against the limit', () => {
    renderAbout()

    expect(screen.getByText(`${CONTEXT.about.length} / ${CONTEXT.about_limit}`)).toBeInTheDocument()
  })

  it.each<ErrorMessage['code'] | undefined>([undefined, 'context_unreadable'])('should show no error for %s', (errorCode) => {
    renderAbout(CONTEXT, errorCode)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should say when notes and documents are full', () => {
    renderAbout(CONTEXT, 'context_full')

    expect(screen.getByRole('alert')).toHaveTextContent('Your notes and documents are full. Remove a document first.')
  })
})
