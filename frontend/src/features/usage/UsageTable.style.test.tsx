import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import styles from '@/features/usage/usage.module.css'
import { TODAY, usage } from '@/features/usage/usage.samples'
import { UsageTable } from '@/features/usage/UsageTable'
import { attachStylesheet } from '@/test/stylesheet'

const COLUMN_GAP = '12px'

attachStylesheet('src/features/usage/usage.module.css', styles)

describe('UsageTable styles', () => {
  it('should keep the column gap between Tokens and Cost', () => {
    render(<UsageTable usage={usage} today={TODAY} />)

    expect(getComputedStyle(screen.getByRole('cell', { name: '210k' })).paddingRight).toBe(COLUMN_GAP)
  })
})
