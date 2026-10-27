import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ProgressBar } from '@/shared/ui/ProgressBar'
import styles from '@/shared/ui/ProgressBar.module.css'

describe('ProgressBar', () => {
  it('should report the percent to screen readers', () => {
    render(<ProgressBar label="Run progress" percent={40} />)

    expect(screen.getByRole('progressbar', { name: 'Run progress' })).toHaveAttribute('aria-valuenow', '40')
  })

  it('should fill the bar to the percent', () => {
    render(<ProgressBar label="Run progress" percent={40} />)

    expect(screen.getByRole('progressbar', { name: 'Run progress' }).firstElementChild).toHaveStyle({ width: '40%' })
  })

  it('should fill in the ok colour when no tone is given', () => {
    render(<ProgressBar label="Run progress" percent={40} />)

    expect(screen.getByRole('progressbar', { name: 'Run progress' }).firstElementChild?.className).toBe(styles.ok)
  })

  it.each([
    ['no class is given', undefined, `${styles.track}`],
    ['a class is given', 'extra', `${styles.track} extra`],
  ])('should class the track when %s', (_, className, expected) => {
    render(<ProgressBar label="Run progress" percent={40} className={className} />)

    expect(screen.getByRole('progressbar', { name: 'Run progress' }).className).toBe(expected)
  })
})
