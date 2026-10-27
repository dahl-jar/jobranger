import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { UsageChart } from '@/features/usage/UsageChart'
import { TODAY, usage } from '@/features/usage/usage.samples'
import { CHART_DAYS, summarizeUsage } from '@/features/usage/usageSummary'

const summary = summarizeUsage(usage, TODAY, null)

function renderChart() {
  return render(
    <UsageChart
      bars={summary.bars}
      label={summary.chartLabel}
      peakTokens={summary.peakTokens}
      firstDay={summary.firstDay}
    />,
  )
}

describe('UsageChart', () => {
  it('should name each bar by day, tokens and cost', () => {
    renderChart()

    expect(screen.getByRole('img', { name: 'Today, 210k tokens, $0.41' })).toBeInTheDocument()
  })

  it('should draw the peak day at full height', () => {
    renderChart()

    expect(screen.getByRole('img', { name: 'Today, 210k tokens, $0.41' }).firstElementChild).toHaveStyle({ height: '56px' })
  })

  it('should let keyboard users reach each bar', async () => {
    renderChart()

    for (let press = 0; press < CHART_DAYS; press += 1) await userEvent.tab()

    expect(screen.getByRole('img', { name: 'Today, 210k tokens, $0.41' })).toHaveFocus()
  })
})
