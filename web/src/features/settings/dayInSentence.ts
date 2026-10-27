import { formatDay } from '@/shared/format/dates'

const RELATIVE_DAYS: ReadonlySet<string> = new Set(['Today', 'Yesterday'])

export function dayInSentence(iso: string, today: string): string {
  const day = formatDay(iso, today)
  return RELATIVE_DAYS.has(day) ? day.toLowerCase() : day
}
