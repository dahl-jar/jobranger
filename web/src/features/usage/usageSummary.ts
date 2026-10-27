import type { Day, Money, UsageDay, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import { formatDay, shiftDay } from '@/shared/format/dates'
import { formatUsd } from '@/shared/format/money'
import { formatTokens } from '@/shared/format/tokens'
import { PERCENT_MAX } from '@/shared/ui/ProgressBar'

export const CHART_DAYS = 14
export const TABLE_DAYS = 7
const BAR_MAX_PX = 56
const BAR_MIN_PX = 2
const NEAR_LIMIT_SHARE = 0.8

type DaysByKey = ReadonlyMap<Day, UsageDay>

export interface UsageBar {
  key: Day
  day: string
  tokens: string
  usd: string
  height: number
  isToday: boolean
  tip: string
}

interface UsageRow {
  key: Day
  day: string
  adsRead: number
  adsChecked: number
  tokens: string
  usd: string
}

interface UsageBudget {
  line: string
  percent: number
  nearLimit: boolean
  action: 'Set a limit' | 'Edit limit'
}

interface UsageSummary {
  monthUsd: string
  todayUsd: string
  averageUsd: string
  peakUsd: string
  peakTokens: string
  budget: UsageBudget
  resets: string
  bars: UsageBar[]
  chartLabel: string
  firstDay: string
}

interface UsageTotals {
  monthUsd: string
  monthTokens: string
  todayUsd: string
  todayTokens: string
}

function lastDays(today: Day, count: number): Day[] {
  return Array.from({ length: count }, (_, index) => shiftDay(today, index - count + 1))
}

function emptyDay(day: Day): UsageDay {
  return { day, calls: 0, input_tokens: 0, output_tokens: 0, cost_usd: 0, ads_read: 0, ads_checked: 0 }
}

function dayTokens(day: UsageDay): number {
  return day.input_tokens + day.output_tokens
}

function indexDays(usage: UsageSnapshot): DaysByKey {
  return new Map(usage.days.map((day) => [day.day, day]))
}

function summarizeBudget(spent: Money, limit: Money | null): UsageBudget {
  if (limit === null) return { line: 'No monthly limit', percent: 0, nearLimit: false, action: 'Set a limit' }
  const share = limit > 0 ? spent / limit : 1
  return {
    line: `${formatUsd(spent)} of ${formatUsd(limit)}`,
    percent: Math.min(PERCENT_MAX, Math.round(share * PERCENT_MAX)),
    nearLimit: share > NEAR_LIMIT_SHARE,
    action: 'Edit limit',
  }
}

function averageCost(chartDays: UsageDay[], byDay: DaysByKey): number {
  const firstIndex = chartDays.findIndex((day) => byDay.has(day.day))
  const tracked = chartDays.slice(Math.max(firstIndex, 0))
  return tracked.reduce((sum, day) => sum + day.cost_usd, 0) / tracked.length
}

function toBar(day: UsageDay, peakTokens: number, today: Day): UsageBar {
  const count = dayTokens(day)
  const scaled = peakTokens > 0 ? Math.round((count / peakTokens) * BAR_MAX_PX) : 0
  const label = formatDay(day.day, today)
  const tokens = formatTokens(count)
  const usd = formatUsd(day.cost_usd)
  return {
    key: day.day,
    day: label,
    tokens,
    usd,
    height: Math.max(BAR_MIN_PX, scaled),
    isToday: day.day === today,
    tip: `${label}, ${tokens} tokens, ${usd}`,
  }
}

function toRow(day: UsageDay, today: Day): UsageRow {
  return {
    key: day.day,
    day: formatDay(day.day, today),
    adsRead: day.ads_read,
    adsChecked: day.ads_checked,
    tokens: formatTokens(dayTokens(day)),
    usd: formatUsd(day.cost_usd),
  }
}

export function summarizeUsage(usage: UsageSnapshot, today: Day, monthlyLimitUsd: Money | null): UsageSummary {
  const byDay = indexDays(usage)
  const chartDays = lastDays(today, CHART_DAYS).map((day) => byDay.get(day) ?? emptyDay(day))
  const tokens = chartDays.map(dayTokens)
  const peakTokens = Math.max(...tokens)
  const peakCost = Math.max(...chartDays.map((day) => day.cost_usd))
  return {
    monthUsd: formatUsd(usage.month_usd),
    todayUsd: formatUsd(usage.today_usd),
    averageUsd: formatUsd(averageCost(chartDays, byDay)),
    peakUsd: formatUsd(peakCost),
    peakTokens: formatTokens(peakTokens),
    budget: summarizeBudget(usage.month_usd, monthlyLimitUsd),
    resets: `Resets ${formatDay(usage.resets_on, today)}`,
    bars: chartDays.map((day) => toBar(day, peakTokens, today)),
    chartLabel: `Tokens per day for the last ${CHART_DAYS} days, from ${formatTokens(Math.min(...tokens))} to ${formatTokens(peakTokens)}`,
    firstDay: formatDay(shiftDay(today, 1 - CHART_DAYS), today),
  }
}

export function recentUsage(usage: UsageSnapshot, today: Day): UsageRow[] {
  const byDay = indexDays(usage)
  return lastDays(today, TABLE_DAYS)
    .flatMap((day) => byDay.get(day) ?? [])
    .reverse()
    .map((day) => toRow(day, today))
}

export function usageTotals(usage: UsageSnapshot): UsageTotals {
  return {
    monthUsd: formatUsd(usage.month_usd),
    monthTokens: formatTokens(usage.month_tokens),
    todayUsd: formatUsd(usage.today_usd),
    todayTokens: formatTokens(usage.today_tokens),
  }
}
