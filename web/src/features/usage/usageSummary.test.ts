import { describe, expect, it } from 'vitest'
import { pricedDays, TODAY, today, usage, yesterday } from '@/features/usage/usage.samples'
import { recentUsage, summarizeUsage, usageTotals } from '@/features/usage/usageSummary'

const CHART_DAYS = 14
const BAR_MAX_PX = 56
const BAR_MIN_PX = 2

describe('summarizeUsage', () => {
  it('should label each bar with day, tokens and cost', () => {
    const { bars } = summarizeUsage(usage, TODAY, null)

    expect(bars.slice(-2).map((bar) => bar.tip)).toEqual(['Yesterday, 192k tokens, $0.20', 'Today, 210k tokens, $0.41'])
  })

  it('should fill days without usage so the chart covers 14 days', () => {
    const { bars } = summarizeUsage(usage, TODAY, null)

    expect(bars).toHaveLength(CHART_DAYS)
    expect(bars[0]?.height).toBe(BAR_MIN_PX)
  })

  it('should scale bars to the peak day', () => {
    const { bars } = summarizeUsage(usage, TODAY, null)

    expect(bars.slice(-2).map((bar) => bar.height)).toEqual([51, BAR_MAX_PX])
  })

  it('should draw minimum bars when nothing was used', () => {
    const { bars } = summarizeUsage({ ...usage, days: [] }, TODAY, null)

    expect(new Set(bars.map((bar) => bar.height))).toEqual(new Set([BAR_MIN_PX]))
  })

  it('should ignore days outside the 14 day window', () => {
    const old = { ...today, day: '2026-09-01', input_tokens: 9000000 }

    const { bars } = summarizeUsage({ ...usage, days: [old, ...usage.days] }, TODAY, null)

    expect(bars.at(-1)?.height).toBe(BAR_MAX_PX)
  })

  it('should mark only the last bar as today', () => {
    const { bars } = summarizeUsage(usage, TODAY, null)

    const todayBars = bars.map((bar) => bar.isToday)
    expect(todayBars.lastIndexOf(true)).toBe(CHART_DAYS - 1)
    expect(todayBars.indexOf(true)).toBe(CHART_DAYS - 1)
  })

  it('should describe the chart range for screen readers', () => {
    const { chartLabel } = summarizeUsage(usage, TODAY, null)

    expect(chartLabel).toBe('Tokens per day for the last 14 days, from 0 to 210k')
  })

  it('should start the axis 13 days before today', () => {
    const { firstDay } = summarizeUsage(usage, TODAY, null)

    expect(firstDay).toBe('29 Sep')
  })

  it('should average to zero when nothing was recorded', () => {
    const { averageUsd } = summarizeUsage({ ...usage, days: [] }, TODAY, null)

    expect(averageUsd).toBe('$0.00')
  })

  it('should average cost over the days since the first recorded day', () => {
    const { averageUsd } = summarizeUsage({ ...usage, days: pricedDays }, TODAY, null)

    expect(averageUsd).toBe('$0.30')
  })

  it('should show the most expensive day as peak', () => {
    const days = [{ ...yesterday, cost_usd: 0.9 }, today]

    const { peakUsd } = summarizeUsage({ ...usage, days }, TODAY, null)

    expect(peakUsd).toBe('$0.90')
  })

  it.each([
    ['no limit', null, { line: 'No monthly limit', percent: 0, nearLimit: false, action: 'Set a limit' }],
    ['a limit far away', 5, { line: '$2.35 of $5.00', percent: 47, nearLimit: false, action: 'Edit limit' }],
    ['more than 80 percent used', 2.5, { line: '$2.35 of $2.50', percent: 94, nearLimit: true, action: 'Edit limit' }],
    ['the limit passed', 2, { line: '$2.35 of $2.00', percent: 100, nearLimit: true, action: 'Edit limit' }],
  ])('should describe the monthly limit with %s', (_, limit, expected) => {
    const { budget } = summarizeUsage(usage, TODAY, limit)

    expect(budget).toEqual(expected)
  })

  it('should treat a zero limit as used up before anything is spent', () => {
    const { budget } = summarizeUsage({ ...usage, month_usd: 0 }, TODAY, 0)

    expect([budget.percent, budget.nearLimit]).toEqual([100, true])
  })

  it('should not warn at exactly 80 percent of the limit', () => {
    const { budget } = summarizeUsage({ ...usage, month_usd: 4 }, TODAY, 5)

    expect(budget.nearLimit).toBe(false)
  })

  it('should say when the month resets', () => {
    const { resets } = summarizeUsage(usage, TODAY, null)

    expect(resets).toBe('Resets 1 Nov')
  })
})

describe('recentUsage', () => {
  it('should list the last 7 days newest first', () => {
    const old = { ...yesterday, day: '2026-10-05' }

    const rows = recentUsage({ ...usage, days: [old, ...usage.days] }, TODAY)

    expect(rows.map((row) => row.day)).toEqual(['Today', 'Yesterday'])
  })

  it('should show reads, checks, tokens and cost for a recent day', () => {
    const [row] = recentUsage(usage, TODAY)

    expect(row).toEqual({ key: TODAY, day: 'Today', adsRead: 812, adsChecked: 41, tokens: '210k', usd: '$0.41' })
  })
})

describe('usageTotals', () => {
  it('should show tokens and cost for the month and today', () => {
    const totals = usageTotals(usage)

    expect(totals).toEqual({ monthUsd: '$2.35', monthTokens: '1.8M', todayUsd: '$0.41', todayTokens: '210k' })
  })
})
