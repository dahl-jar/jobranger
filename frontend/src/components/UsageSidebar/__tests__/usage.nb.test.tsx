import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UsageSidebar } from '@/components/UsageSidebar/UsageSidebar'
import { UsageTable } from '@/components/UsageTable/UsageTable'
import { usage } from '@/__tests__/usage.samples'
import { TODAY } from '@/__tests__/testClock'
import { expectNoEnglish } from '@/__tests__/findEnglishText'
import { renderNorwegian } from '@/__tests__/renderWithProviders'

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

    expect(await screen.findByText(/^Denne måneden/)).toBeInTheDocument()
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
