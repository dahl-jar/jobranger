import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { ErrorMessage, GuidanceSnapshot } from '@/features/relay/contract/payload.gen'
import { MatchingSettings } from '@/features/settings/MatchingSettings'

const EDITED = payloadFixtures['guidance.snapshot']
const DEFAULT = { ...EDITED, text: '', is_default: true }

function renderMatching(guidance: GuidanceSnapshot = EDITED, errorCode?: ErrorMessage['code'], editsDisabled = false) {
  const onSave = vi.fn()
  const onReset = vi.fn()
  const view = render(
    <MatchingSettings guidance={guidance} errorCode={errorCode} editsDisabled={editsDisabled} onSave={onSave} onReset={onReset} />,
  )
  return { ...view, onSave, onReset, user: userEvent.setup() }
}

async function openEditor(guidance: GuidanceSnapshot = EDITED) {
  const rendered = renderMatching(guidance)
  await rendered.user.click(screen.getByRole('button', { name: 'Edit matching guidance' }))
  return rendered
}

describe('MatchingSettings', () => {
  it.each([
    { name: 'edited', guidance: EDITED, state: 'Edited' },
    { name: 'default', guidance: DEFAULT, state: 'Default' },
  ])('should label $name guidance $state', ({ guidance, state }) => {
    renderMatching(guidance)

    expect(screen.getByText(state)).toBeInTheDocument()
  })

  it('should open the editor with the saved guidance', async () => {
    await openEditor()

    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveValue(EDITED.text)
    expect(screen.getByRole('button', { name: 'Close matching guidance' })).toHaveAttribute('aria-expanded', 'true')
  })

  it('should start empty for the default guidance', async () => {
    await openEditor(DEFAULT)

    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveValue('')
    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveAttribute('placeholder', 'Add your own matching guidance')
  })

  it('should send the trimmed guidance on Save', async () => {
    const { onSave, user } = await openEditor(DEFAULT)
    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), '  Skip consulting roles.  ')

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(onSave).toHaveBeenCalledExactlyOnceWith('Skip consulting roles.')
  })

  it('should disable Save when the text passes the limit', async () => {
    const { user } = await openEditor({ ...EDITED, limit: EDITED.text.length })

    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), '!')

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(screen.getByText(`${EDITED.text.length + 1} / ${EDITED.text.length}`)).toBeInTheDocument()
  })

  it('should allow Save at exactly the limit', async () => {
    const { user } = await openEditor({ ...EDITED, limit: EDITED.text.length + 1 })

    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), '!')

    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })

  it('should show no error before the machine replies', async () => {
    await openEditor()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should disable Save when nothing changed', async () => {
    await openEditor()

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('should disable Save when the text is blank', async () => {
    const { user } = await openEditor()

    await user.clear(screen.getByRole('textbox', { name: 'Matching guidance' }))
    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), '   ')

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('should reset the guidance on Reset to default', async () => {
    const { onReset, onSave, user } = await openEditor()

    await user.click(screen.getByRole('button', { name: 'Reset to default' }))

    expect(onReset).toHaveBeenCalledExactlyOnceWith()
    expect(onSave).not.toHaveBeenCalled()
  })

  it('should disable Reset to default for the default guidance', async () => {
    await openEditor(DEFAULT)

    expect(screen.getByRole('button', { name: 'Reset to default' })).toBeDisabled()
  })

  it('should move focus to the guidance box on Save', async () => {
    const { user } = await openEditor()
    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), ' More.')

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveFocus()
  })

  it('should keep the draft when the machine says the guidance is empty', async () => {
    const { rerender, onSave, onReset, user } = await openEditor()
    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), ' More.')

    rerender(<MatchingSettings guidance={EDITED} errorCode="guidance_empty" editsDisabled={false} onSave={onSave} onReset={onReset} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Write some guidance or reset to the default.')
    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveValue(`${EDITED.text} More.`)
  })

  it('should describe the guidance box with its character count', async () => {
    await openEditor()

    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveAccessibleDescription(`${EDITED.text.length} / ${EDITED.limit}`)
  })

  it('should let the guidance be read while the machine is away', async () => {
    const { user } = renderMatching(EDITED, undefined, true)

    await user.click(screen.getByRole('button', { name: 'Edit matching guidance' }))

    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveAttribute('readonly')
    expect(screen.getByRole('textbox', { name: 'Matching guidance' })).toHaveValue(EDITED.text)
  })

  it('should disable Reset to default while the machine is away', async () => {
    const { user } = renderMatching(EDITED, undefined, true)

    await user.click(screen.getByRole('button', { name: 'Edit matching guidance' }))

    expect(screen.getByRole('button', { name: 'Reset to default' })).toBeDisabled()
  })

  it('should disable Save on a draft when the machine goes away', async () => {
    const { rerender, onSave, onReset, user } = await openEditor()
    await user.type(screen.getByRole('textbox', { name: 'Matching guidance' }), ' More.')

    rerender(<MatchingSettings guidance={EDITED} errorCode={undefined} editsDisabled onSave={onSave} onReset={onReset} />)

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('should close the editor', async () => {
    const { user } = await openEditor()

    await user.click(screen.getByRole('button', { name: 'Close matching guidance' }))

    expect(screen.queryByRole('textbox', { name: 'Matching guidance' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit matching guidance' })).toHaveAttribute('aria-expanded', 'false')
  })
})
