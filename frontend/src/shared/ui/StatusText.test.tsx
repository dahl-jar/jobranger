import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Icon } from '@/shared/ui/Icon'
import { StatusText } from '@/shared/ui/StatusText'
import styles from '@/shared/ui/StatusText.module.css'

describe('StatusText', () => {
  it('should announce the text in a status region', () => {
    render(<StatusText kind="saved">Saved</StatusText>)

    expect(screen.getByRole('status')).toHaveTextContent('Saved')
  })

  it('should keep an empty status region when there is no status', () => {
    render(<StatusText kind={undefined}>Saved</StatusText>)

    const status = screen.getByRole('status')
    expect(status).toBeEmptyDOMElement()
    expect(status.className).toBe(styles.status)
  })

  it.each([
    { kind: 'saved', icon: 'check' },
    { kind: 'error', icon: 'close' },
  ] as const)('should mark $kind with the $icon icon', ({ kind, icon }) => {
    const { container } = render(<Icon name={icon} />)
    const expected = container.innerHTML
    container.remove()

    render(<StatusText kind={kind}>Text</StatusText>)

    const status = screen.getByRole('status')
    expect(status.querySelector('svg')?.outerHTML).toBe(expected)
    expect(status.className).toBe(`${styles.status} ${styles[kind]}`)
  })
})
