import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UsageSidebar } from '@/features/usage/UsageSidebar'
import { pricedDays, TODAY, usage } from '@/features/usage/usage.samples'
import type { Money, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import bar from '@/shared/ui/ProgressBar.module.css'
import { renderRoutes } from '@/test/render'

function renderSidebar(monthlyLimitUsd: Money | null, snapshot: UsageSnapshot = usage) {
  return renderRoutes(
    [{ path: '/', element: <UsageSidebar usage={snapshot} today={TODAY} monthlyLimitUsd={monthlyLimitUsd} /> }],
    '/',
  )
}

describe('UsageSidebar', () => {
  it('should show the share of the monthly limit used', () => {
    renderSidebar(5)

    expect(screen.getByRole('progressbar', { name: 'Monthly limit used' })).toHaveAttribute('aria-valuenow', '47')
  })

  it.each([
    ['more than 80 percent is used', 2.5, bar.near],
    ['less than 80 percent is used', 5, bar.ok],
  ])('should colour the limit bar by its distance to the limit when %s', (_, limit, tone) => {
    renderSidebar(limit)

    expect(screen.getByRole('progressbar', { name: 'Monthly limit used' }).firstElementChild?.className).toBe(tone)
  })

  it('should link Edit limit to model settings when a limit is set', () => {
    renderSidebar(5)

    expect(screen.getByRole('link', { name: 'Edit limit' })).toHaveAttribute('href', '/app/settings/model')
  })

  it('should show cost for today, the daily average and the peak day', () => {
    renderSidebar(null, { ...usage, days: pricedDays })

    const figures = screen.getAllByRole('definition').map((figure) => figure.textContent)
    expect(figures).toEqual(['$0.41', '$0.30', '$0.40'])
  })
})
