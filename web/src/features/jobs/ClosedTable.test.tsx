import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ClosedTable } from '@/features/jobs/ClosedTable'
import styles from '@/features/jobs/tables.module.css'
import { ADS_SNAPSHOT, CLOSED_AD } from '@/features/jobs/goldenPayloads'
import type { ClosedAd } from '@/features/relay/contract/payload.gen'

function renderTable(ad: ClosedAd) {
  render(<ClosedTable snapshot={{ ...ADS_SNAPSHOT, closed: [ad] }} />)
}

function cellOf(column: number) {
  const [, row] = screen.getAllByRole('row')
  if (row === undefined) throw new Error('the table has no ad row')
  return within(row).getAllByRole('cell')[column]
}

const WHY_COLUMN = 3
const YOU_COLUMN = 4

describe('ClosedTable', () => {
  it.each([
    ['applied', 'Applied'],
    ['skipped', 'Skipped'],
    ['not_for_me', 'Not for me'],
    ['none', 'No action'],
  ] as const)('should show %s as %s in the You column', (mark, text) => {
    renderTable({ ...CLOSED_AD, mark })

    expect(cellOf(YOU_COLUMN)).toHaveTextContent(text)
  })

  it.each([
    ['deadline', 'Deadline passed'],
    ['removed', 'Removed from FINN'],
    ['filled', 'Filled, ad says so'],
  ] as const)('should explain a %s close as %s', (reason, text) => {
    renderTable({ ...CLOSED_AD, reason })

    expect(cellOf(WHY_COLUMN)).toHaveTextContent(text)
  })

  it('should show the day the ad closed', () => {
    renderTable(CLOSED_AD)

    expect(cellOf(0)).toHaveTextContent('8 Oct')
  })

  it('should set an applied ad apart in the You column', () => {
    renderTable({ ...CLOSED_AD, mark: 'applied' })

    expect(cellOf(YOU_COLUMN)).toHaveClass(styles.youApplied ?? '')
  })

  it('should leave a skipped ad plain in the You column', () => {
    renderTable({ ...CLOSED_AD, mark: 'skipped' })

    expect(cellOf(YOU_COLUMN)).not.toHaveClass(styles.youApplied ?? '')
  })
})
