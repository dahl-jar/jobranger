import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { HiddenSummary } from '@/features/jobs/HiddenSummary'
import type { HiddenBreakdown } from '@/features/jobs/selectors'

const BREAKDOWN: HiddenBreakdown = {
  total: 41,
  parts: [
    { kind: 'senior', label: 'senior', count: 18 },
    { kind: 'years', label: 'need 3+ yrs', count: 12 },
  ],
}

describe('HiddenSummary', () => {
  it('should sum up hidden ads by reason', () => {
    render(
      <MemoryRouter>
        <HiddenSummary breakdown={BREAKDOWN} />
      </MemoryRouter>,
    )

    expect(screen.getByText(/41 hidden: 18 senior · 12 need 3\+ yrs/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'view' })).toHaveAttribute('href', '/app/hidden')
  })

  it('should show nothing when no ads are hidden', () => {
    const { container } = render(
      <MemoryRouter>
        <HiddenSummary breakdown={{ total: 0, parts: [] }} />
      </MemoryRouter>,
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('should give the total alone when no reasons are listed', () => {
    render(
      <MemoryRouter>
        <HiddenSummary breakdown={{ total: 41, parts: [] }} />
      </MemoryRouter>,
    )

    expect(screen.getByText(/^41 hidden ·/)).toBeInTheDocument()
  })
})
