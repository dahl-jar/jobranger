import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CheckList, type Check } from '@/shared/ui/CheckList'
import { Icon } from '@/shared/ui/Icon'
import styles from '@/shared/ui/marks.module.css'

const CHECK: Check = { text: 'React', state: 'met' }

describe('CheckList', () => {
  it.each([
    { state: 'met', icon: 'check' },
    { state: 'partly', icon: 'dash' },
    { state: 'missing', icon: 'close' },
  ] as const)('should mark a $state check with the $icon icon', ({ state, icon }) => {
    const { container } = render(<Icon name={icon} />)
    const expected = container.innerHTML
    container.remove()

    render(<CheckList checks={[{ ...CHECK, state }]} />)

    const mark = screen.getByRole('listitem').querySelector('span')
    expect(mark?.innerHTML).toBe(expected)
    expect(mark?.className).toBe(styles[state])
  })

  it('should show the state word when showWord is set', () => {
    render(<CheckList checks={[CHECK]} showWord />)

    expect(within(screen.getByRole('listitem')).getByText('met').className).toBe(styles.word)
  })

  it('should hide the word visually when showWord is not set', () => {
    render(<CheckList checks={[CHECK]} />)

    const item = screen.getByRole('listitem')
    expect(item.textContent).toBe('React met')
    expect(within(item).getByText('met').className).toBe('visually-hidden')
  })
})
