import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { ConfigSummary } from '@/features/jobs/ConfigSummary'
import { CONFIG_SNAPSHOT } from '@/features/jobs/goldenPayloads'

function renderSummary() {
  render(
    <MemoryRouter>
      <ConfigSummary targets={CONFIG_SNAPSHOT.targets} />
    </MemoryRouter>,
  )
}

describe('ConfigSummary', () => {
  it.each([
    ['Level', 'Junior, Mid-level'],
    ['Experience asked', '0–1 years, 1–3 years'],
    ["Master's", 'Not required'],
    ['Places', 'Bergen, Oslo'],
  ])('should show %s as %s', (label, value) => {
    renderSummary()

    expect(screen.getByText(label).nextElementSibling).toHaveTextContent(value)
  })

  it('should link Edit to search settings', () => {
    renderSummary()

    expect(screen.getByRole('link', { name: 'Edit config' })).toHaveAttribute('href', '/app/settings/search')
  })
})
