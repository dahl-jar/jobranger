import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FitMark } from '@/shared/ui/FitMark'
import { Icon } from '@/shared/ui/Icon'
import styles from '@/shared/ui/marks.module.css'

describe('FitMark', () => {
  it.each([
    { fit: 'strong', icon: 'strong', words: 'Strong fit' },
    { fit: 'partial', icon: 'partial', words: 'Partial fit' },
    { fit: 'on_request', icon: 'eye', words: 'Shown on request' },
  ] as const)('should mark $fit with the $icon icon and "$words"', ({ fit, icon, words }) => {
    const { container } = render(<Icon name={icon} />)
    const expected = container.innerHTML
    container.remove()

    render(<FitMark fit={fit} />)

    const mark = screen.getByText(words)
    expect(mark.querySelector('svg')?.outerHTML).toBe(expected)
    expect(mark.className).toBe(`${styles.fit} ${styles[fit]}`)
  })
})
