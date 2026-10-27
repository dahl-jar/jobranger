import type { Day, Money, UsageDay, UsageSnapshot } from '@/features/relay/contract/payload.gen'
import { formatDay, shiftDay } from '@/shared/format/dates'
import { formatUsd } from '@/shared/format/money'
import { formatTokens } from '@/shared/format/tokens'
import { en, type Messages } from '@/shared/i18n/en'
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
  action: string
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

function limitShare(spent: Money, limit: Money): number {
  return limit > 0 ? spent / limit : 1
}

export function limitPercent(spent: Money, limit: Money): number {
  return Math.min(PERCENT_MAX, Math.round(limitShare(spent, limit) * PERCENT_MAX))
}

function summarizeBudget(spent: Money, limit: Money | null, text: Messages): UsageBudget {
  const words = text.usage
  if (limit === null) return { line: words.noLimit, percent: 0, nearLimit: false, action: words.setLimit }
  return {
    line: words.ofLimit(formatUsd(spent, text), formatUsd(limit, text)),
    percent: limitPercent(spent, limit),
    nearLimit: limitShare(spent, limit) > NEAR_LIMIT_SHARE,
    action: words.editLimit,
  }
}

function averageCost(chartDays: UsageDay[], byDay: DaysByKey): number {
  const firstIndex = chartDays.findIndex((day) => byDay.has(day.day))
  const tracked = chartDays.slice(Math.max(firstIndex, 0))
  return tracked.reduce((sum, day) => sum + day.cost_usd, 0) / tracked.length
}

function toBar(day: UsageDay, peakTokens: number, today: Day, text: Messages): UsageBar {
  const count = dayTokens(day)
  const scaled = peakTokens > 0 ? Math.round((count / peakTokens) * BAR_MAX_PX) : 0
  const label = formatDay(day.day, today, text)
  const tokens = formatTokens(count, text)
  const usd = formatUsd(day.cost_usd, text)
  return {
    key: day.day,
    day: label,
    tokens,
    usd,
    height: Math.max(BAR_MIN_PX, scaled),
    isToday: day.day === today,
    tip: text.usage.tip(label, count, tokens, usd),
  }
}

function toRow(day: UsageDay, today: Day, text: Messages): UsageRow {
  return {
    key: day.day,
    day: formatDay(day.day, today, text),
    adsRead: day.ads_read,
    adsChecked: day.ads_checked,
    tokens: formatTokens(dayTokens(day), text),
    usd: formatUsd(day.cost_usd, text),
  }
}

export function summarizeUsage(usage: UsageSnapshot, today: Day, monthlyLimitUsd: Money | null, text: Messages = en): UsageSummary {
  const byDay = indexDays(usage)
  const chartDays = lastDays(today, CHART_DAYS).map((day) => byDay.get(day) ?? emptyDay(day))
  const tokens = chartDays.map(dayTokens)
  const peakTokens = Math.max(...tokens)
  const peakCost = Math.max(...chartDays.map((day) => day.cost_usd))
  return {
    monthUsd: formatUsd(usage.month_usd, text),
    todayUsd: formatUsd(usage.today_usd, text),
    averageUsd: formatUsd(averageCost(chartDays, byDay), text),
    peakUsd: formatUsd(peakCost, text),
    peakTokens: formatTokens(peakTokens, text),
    budget: summarizeBudget(usage.month_usd, monthlyLimitUsd, text),
    resets: text.usage.resets(formatDay(usage.resets_on, today, text)),
    bars: chartDays.map((day) => toBar(day, peakTokens, today, text)),
    chartLabel: text.usage.chartLabel(CHART_DAYS, formatTokens(Math.min(...tokens), text), formatTokens(peakTokens, text)),
    firstDay: formatDay(shiftDay(today, 1 - CHART_DAYS), today, text),
  }
}

export function recentUsage(usage: UsageSnapshot, today: Day, text: Messages = en): UsageRow[] {
  const byDay = indexDays(usage)
  return lastDays(today, TABLE_DAYS)
    .flatMap((day) => byDay.get(day) ?? [])
    .reverse()
    .map((day) => toRow(day, today, text))
}

export function usageTotals(usage: UsageSnapshot, text: Messages = en): UsageTotals {
  return {
    monthUsd: formatUsd(usage.month_usd, text),
    monthTokens: formatTokens(usage.month_tokens, text),
    todayUsd: formatUsd(usage.today_usd, text),
    todayTokens: formatTokens(usage.today_tokens, text),
  }
}
