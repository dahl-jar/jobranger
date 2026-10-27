const LOCALE = 'en-GB'
const DAY_MS = 86_400_000
const DAY_TEXT_LENGTH = 10
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'] as const

interface Due {
  due: string
  note: string
}

const NO_DEADLINE: Due = { due: 'No date', note: 'apply early' }

function isRealDay(day: string): boolean {
  const utc = new Date(`${day}T00:00Z`)
  return !Number.isNaN(utc.getTime()) && utc.toISOString().slice(0, DAY_TEXT_LENGTH) === day
}

function toLocalDate(iso: string): Date | undefined {
  if (!isRealDay(iso.slice(0, DAY_TEXT_LENGTH))) {
    return undefined
  }
  const date = new Date(iso.length === DAY_TEXT_LENGTH ? `${iso}T00:00` : iso)
  return Number.isNaN(date.getTime()) ? undefined : date
}

function formatDayMonth(date: Date): string {
  return `${date.getDate()} ${MONTHS[date.getMonth()] ?? ''}`
}

function daysBetween(from: Date, to: Date): number {
  const fromDay = Date.UTC(from.getFullYear(), from.getMonth(), from.getDate())
  const toDay = Date.UTC(to.getFullYear(), to.getMonth(), to.getDate())
  return Math.round((toDay - fromDay) / DAY_MS)
}

function dueNote(days: number): string {
  if (days < 0) {
    return 'passed'
  }
  if (days === 0) {
    return 'today'
  }
  if (days === 1) {
    return 'tomorrow'
  }
  return `in ${days} days`
}

export function formatDue(deadline: string | null | undefined, today: string): Due {
  const date = deadline === null || deadline === undefined ? undefined : toLocalDate(deadline)
  if (date === undefined) {
    return NO_DEADLINE
  }
  const from = toLocalDate(today)
  return { due: formatDayMonth(date), note: from === undefined ? '' : dueNote(daysBetween(from, date)) }
}

export function formatDay(iso: string, today: string): string {
  const date = toLocalDate(iso)
  if (date === undefined) {
    return ''
  }
  const from = toLocalDate(today)
  const days = from === undefined ? undefined : daysBetween(from, date)
  if (days === 0) {
    return 'Today'
  }
  if (days === -1) {
    return 'Yesterday'
  }
  return formatDayMonth(date)
}

export function shiftDay(day: string, offset: number): string {
  return new Date(Date.parse(day) + offset * DAY_MS).toISOString().slice(0, DAY_TEXT_LENGTH)
}

export function formatClock(iso: string): string {
  const date = toLocalDate(iso)
  return date === undefined ? '' : date.toLocaleTimeString(LOCALE, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
}
