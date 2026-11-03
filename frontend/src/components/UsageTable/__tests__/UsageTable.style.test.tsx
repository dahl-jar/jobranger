import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import styles from '@/components/UsageViewStyles/usageViews.module.css'
import { usage } from '@/__tests__/usage.samples'
import { TODAY } from '@/__tests__/testClock'
import { UsageTable } from '@/components/UsageTable/UsageTable'
import { attachStylesheet } from '@/__tests__/attachStylesheet'

const COLUMN_GAP = '12px'

attachStylesheet('src/components/UsageViewStyles/usageViews.module.css', styles)

describe('UsageTable styles', () => {
  it('should keep the column gap between Tokens and Cost', () => {
    render(<UsageTable usage={usage} today={TODAY} />)

    expect(getComputedStyle(screen.getByRole('cell', { name: '210k' })).paddingRight).toBe(COLUMN_GAP)
  })
})
