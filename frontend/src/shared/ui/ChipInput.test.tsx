import { fireEvent, render, screen } from '@testing-library/react'
import { useState, type FormEvent } from 'react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChipInput } from '@/shared/ui/ChipInput'
import styles from '@/shared/ui/ChipInput.module.css'

const PLACES = ['Oslo', 'Bergen', 'Tromsø']

function renderPlaces(items: readonly string[] = PLACES, limits: { maxItems?: number; maxLength?: number } = {}) {
  const onChange = vi.fn<(items: string[]) => void>()
  render(<ChipInput id="places" label="Places" items={items} onChange={onChange} {...limits} />)
  return { onChange, input: screen.getByLabelText<HTMLInputElement>('Places') }
}

function StatefulPlaces({ initial }: { initial: readonly string[] }) {
  const [items, setItems] = useState<readonly string[]>(initial)
  return <ChipInput id="places" label="Places" items={items} onChange={setItems} />
}

describe('ChipInput', () => {
  it('should add a draft with spaces on Enter', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, 'Mo i Rana{Enter}')

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Mo i Rana']]])
  })

  it('should add the draft on comma', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, 'Bodø,')

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Bodø']]])
  })

  it('should clear the draft after adding', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces()

    await user.type(input, 'Bodø{Enter}')

    expect(input).toHaveValue('')
  })

  it('should skip a place already listed in another case', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, 'oslo{Enter}')

    expect(onChange).not.toHaveBeenCalled()
    expect(input).toHaveValue('')
  })

  it('should skip a blank draft', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, '   {Enter}')

    expect(onChange).not.toHaveBeenCalled()
  })

  it('should not submit the surrounding form on Enter', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn((event: FormEvent) => event.preventDefault())
    render(
      <form onSubmit={onSubmit}>
        <ChipInput id="places" label="Places" items={PLACES} onChange={() => undefined} />
      </form>,
    )

    await user.type(screen.getByLabelText('Places'), '{Enter}')

    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('should remove the last chip on Backspace when the draft is empty', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, '{Backspace}')

    expect(onChange.mock.calls).toEqual([[['Oslo', 'Bergen']]])
  })

  it('should leave an empty list alone on Backspace', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces([])

    await user.type(input, '{Backspace}')

    expect(onChange).not.toHaveBeenCalled()
  })

  it('should keep the chips on Backspace while the draft has text', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, 'Bo{Backspace}')

    expect(onChange).not.toHaveBeenCalled()
    expect(input).toHaveValue('B')
  })

  it('should remove a chip with its remove button', async () => {
    const user = userEvent.setup()
    const { onChange } = renderPlaces()

    await user.click(screen.getByRole('button', { name: 'Remove Oslo' }))

    expect(onChange.mock.calls).toEqual([[['Bergen', 'Tromsø']]])
  })

  it.each([
    { name: 'isComposing', init: { isComposing: true } },
    { name: 'key code 229', init: { keyCode: 229 } },
  ])('should not add a chip on Enter while an IME composes ($name)', ({ init }) => {
    const { onChange, input } = renderPlaces()
    fireEvent.change(input, { target: { value: 'とうきょう' } })

    fireEvent.keyDown(input, { key: 'Enter', ...init })

    expect(onChange).not.toHaveBeenCalled()
    expect(input).toHaveValue('とうきょう')
  })

  it.each([
    { removed: 'Oslo', focused: 'Remove Bergen' },
    { removed: 'Tromsø', focused: 'Remove Bergen' },
  ])('should focus $focused after removing $removed', async ({ removed, focused }) => {
    const user = userEvent.setup()
    render(<StatefulPlaces initial={PLACES} />)
    screen.getByRole('button', { name: `Remove ${removed}` }).focus()

    await user.keyboard('{Enter}')

    expect(screen.getByRole('button', { name: focused })).toHaveFocus()
  })

  it('should focus the input after removing the last chip', async () => {
    const user = userEvent.setup()
    render(<StatefulPlaces initial={['Oslo']} />)
    screen.getByRole('button', { name: 'Remove Oslo' }).focus()

    await user.keyboard('{Enter}')

    expect(screen.getByLabelText('Places')).toHaveFocus()
  })

  it('should add the draft when the input loses focus', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()

    await user.type(input, 'Bodø')
    await user.tab()

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Bodø']]])
  })

  it('should add the draft as a chip when a chip is removed by mouse', async () => {
    const user = userEvent.setup()
    render(<StatefulPlaces initial={PLACES} />)

    await user.type(screen.getByLabelText('Places'), 'Bodø')
    await user.click(screen.getByRole('button', { name: 'Remove Oslo' }))

    expect(screen.getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual(['Remove Bergen', 'Remove Tromsø', 'Remove Bodø'])
  })

  it('should add each new comma-separated part of a paste', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()
    await user.click(input)

    await user.paste('Bodø, oslo,, Molde ,Bodø,')

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Bodø', 'Molde']]])
    expect(input).toHaveValue('')
  })

  it('should join a paste with the draft around the caret', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()
    await user.type(input, 'Åle')

    await user.paste('sund, Hamar')

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Ålesund']]])
  })

  it('should keep the part after the last comma of a paste as the draft', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces()
    await user.type(input, 'Åle')

    await user.paste('sund, Hamar')

    expect(input).toHaveValue('Hamar')
  })

  it('should replace the selected draft text with a paste', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces()
    await user.type(input, 'Hamar')
    input.setSelectionRange(0, 'Hamar'.length)

    await user.paste('Bodø, Molde')

    expect(input).toHaveValue('Molde')
  })

  it('should split a paste inside the draft at the caret', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()
    await user.type(input, 'Bgen{ArrowLeft}{ArrowLeft}{ArrowLeft}')

    await user.paste('odø, Ber')

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Bodø']]])
    expect(input).toHaveValue('Bergen')
  })

  it('should put the caret after the pasted text when a paste splits the draft', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces()
    await user.type(input, 'Bgen{ArrowLeft}{ArrowLeft}{ArrowLeft}')

    await user.paste('odø, Ber')

    expect(input.selectionStart).toBe('Ber'.length)
  })

  it('should put the caret at the start when a paste trims the draft', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces()
    await user.type(input, ' Molde{Home}')

    await user.paste('Bodø, ')

    expect(input.selectionStart).toBe(0)
  })

  it('should leave the input unfocused after a pointer removal of the last chip', async () => {
    const user = userEvent.setup()
    render(<StatefulPlaces initial={['Oslo']} />)

    await user.click(screen.getByRole('button', { name: 'Remove Oslo' }))

    expect(screen.getByLabelText('Places')).not.toHaveFocus()
  })

  it('should paste text without commas into the draft', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces()
    await user.click(input)

    await user.paste('Bodø')

    expect(onChange).not.toHaveBeenCalled()
    expect(input).toHaveValue('Bodø')
  })

  it('should cut the draft at maxLength', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces(PLACES, { maxLength: 5 })

    await user.type(input, 'Kristiansand')

    expect(input).toHaveValue('Krist')
  })

  it('should add nothing when the list is at maxItems', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces(PLACES, { maxItems: PLACES.length })

    await user.type(input, 'Bodø{Enter}')

    expect(onChange).not.toHaveBeenCalled()
  })

  it('should keep the draft when the list is at maxItems', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces(PLACES, { maxItems: PLACES.length })

    await user.type(input, 'Bodø{Enter}')

    expect(input).toHaveValue('Bodø')
  })

  it('should stop adding pasted parts at maxItems', async () => {
    const user = userEvent.setup()
    const { onChange, input } = renderPlaces(PLACES, { maxItems: PLACES.length + 1 })
    await user.click(input)

    await user.paste('Bodø, Molde, Hamar')

    expect(onChange.mock.calls).toEqual([[[...PLACES, 'Bodø']]])
  })

  it('should keep the pasted parts past maxItems in the draft', async () => {
    const user = userEvent.setup()
    const { input } = renderPlaces(PLACES, { maxItems: PLACES.length + 1 })
    await user.click(input)

    await user.paste('Bodø, Molde, Hamar')

    expect(input).toHaveValue('Molde, Hamar')
  })

  it('should give the root only the field class without a caller class', () => {
    const { container } = render(<ChipInput id="places" label="Places" items={PLACES} onChange={() => undefined} />)

    expect(container.firstElementChild?.className).toBe(styles.field)
  })

  it('should add the caller class to the root', () => {
    const { container } = render(<ChipInput id="places" label="Places" items={PLACES} onChange={() => undefined} className="settings" />)

    expect(container.firstElementChild?.className).toBe(`${styles.field} settings`)
  })
})
