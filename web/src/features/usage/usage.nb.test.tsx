import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UsageSidebar } from '@/features/usage/UsageSidebar'
import { UsageTable } from '@/features/usage/UsageTable'
import { TODAY, usage } from '@/features/usage/usage.samples'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian } from '@/test/render'

describe('usage in bokmål', () => {
  it.each([
    { name: 'a monthly limit', limit: 5 },
    { name: 'no monthly limit', limit: null },
  ])('should show the sidebar with $name without English text', async ({ limit }) => {
    renderNorwegian(<UsageSidebar usage={usage} today={TODAY} monthlyLimitUsd={limit} />)

    expect(await screen.findByRole('heading', { name: 'AI-bruk' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it.each([
    { name: 'recent runs', snapshot: usage },
    { name: 'no recent runs', snapshot: { ...usage, days: [] } },
  ])('should show the table with $name without English text', async ({ snapshot }) => {
    renderNorwegian(<UsageTable usage={snapshot} today={TODAY} />)

    expect(await screen.findByRole('heading', { name: 'Bruk' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should name the daily average with one short word', async () => {
    renderNorwegian(<UsageSidebar usage={usage} today={TODAY} monthlyLimitUsd={null} />)

    expect(await screen.findByText('Dagssnitt')).toBeInTheDocument()
  })

  it('should show money with a decimal comma', async () => {
    renderNorwegian(<UsageTable usage={{ ...usage, month_usd: 1.5 }} today={TODAY} />)

    expect(await screen.findByText('$1,50')).toBeInTheDocument()
  })
})
