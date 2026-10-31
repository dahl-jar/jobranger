import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UsageTable } from '@/features/usage/UsageTable'
import { TODAY, usage } from '@/features/usage/usage.samples'

describe('UsageTable', () => {
  it('should list recent days newest first', () => {
    render(<UsageTable usage={usage} today={TODAY} />)

    const [, first] = screen.getAllByRole('row')
    const cells = within(first as HTMLElement).getAllByRole('cell').map((cell) => cell.textContent)
    expect(cells).toEqual(['Today', '812', '41', '210k', '$0.41'])
  })

  it('should let keyboard users scroll the table when it is too wide', () => {
    render(<UsageTable usage={usage} today={TODAY} />)

    expect(screen.getByRole('region', { name: 'Usage per day' })).toHaveAttribute('tabindex', '0')
  })

  it('should say no runs happened when nothing ran in the last 7 days', () => {
    render(<UsageTable usage={{ ...usage, days: [] }} today={TODAY} />)

    expect(screen.getByText('No runs in the last 7 days.')).toBeInTheDocument()
  })

  it('should show the cost this month and today', () => {
    render(<UsageTable usage={usage} today={TODAY} />)

    const totals = screen.getAllByRole('definition').map((total) => total.textContent)
    expect(totals).toEqual(['$2.35', '$0.41'])
  })
})
