import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Button } from '@/shared/ui/Button'
import styles from '@/shared/ui/button.module.css'

describe('Button', () => {
  it('should not submit a form by default', () => {
    render(<Button>Save</Button>)

    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('type', 'button')
  })

  it('should combine variant, size and caller classes', () => {
    render(
      <Button variant="quiet" size={36} className="extra">
        Cancel
      </Button>,
    )

    expect([...screen.getByRole('button', { name: 'Cancel' }).classList]).toEqual([styles.quiet, styles.size36, 'extra'])
  })

  it('should use the primary variant when none is given', () => {
    render(<Button>Run now</Button>)

    expect(screen.getByRole('button', { name: 'Run now' }).className).toBe(styles.primary)
  })
})
