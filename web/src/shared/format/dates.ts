import { en, type Messages } from '@/shared/i18n/en'

const CLOCK_LOCALE = 'en-GB'
const DAY_MS = 86_400_000
const DAY_TEXT_LENGTH = 10

interface Due {
  due: string
  note: string
}

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

function formatDayMonth(date: Date, text: Messages): string {
  return text.dates.dayMonth(date.getDate(), text.dates.months[date.getMonth()] ?? '')
}

function daysBetween(from: Date, to: Date): number {
  const fromDay = Date.UTC(from.getFullYear(), from.getMonth(), from.getDate())
  const toDay = Date.UTC(to.getFullYear(), to.getMonth(), to.getDate())
  return Math.round((toDay - fromDay) / DAY_MS)
}

function dueNote(days: number, text: Messages): string {
  if (days < 0) {
    return text.dates.passed
  }
  if (days === 0) {
    return text.dates.dueToday
  }
  if (days === 1) {
    return text.dates.dueTomorrow
  }
  return text.dates.dueIn(days)
}

export function formatDue(deadline: string | null | undefined, today: string, text: Messages = en): Due {
  const date = deadline === null || deadline === undefined ? undefined : toLocalDate(deadline)
  if (date === undefined) {
    return { due: text.dates.noDate, note: text.dates.applyEarly }
  }
  const from = toLocalDate(today)
  return { due: formatDayMonth(date, text), note: from === undefined ? '' : dueNote(daysBetween(from, date), text) }
}

export function formatDate(iso: string, text: Messages = en): string {
  const date = toLocalDate(iso)
  return date === undefined ? '' : formatDayMonth(date, text)
}

export function formatDay(iso: string, today: string, text: Messages = en): string {
  const date = toLocalDate(iso)
  if (date === undefined) {
    return ''
  }
  const from = toLocalDate(today)
  const days = from === undefined ? undefined : daysBetween(from, date)
  if (days === 0) {
    return text.dates.today
  }
  if (days === -1) {
    return text.dates.yesterday
  }
  return formatDayMonth(date, text)
}

export function formatDayInSentence(iso: string, today: string, text: Messages = en): string {
  const day = formatDay(iso, today, text)
  return day === text.dates.today || day === text.dates.yesterday ? day.toLowerCase() : day
}

export function shiftDay(day: string, offset: number): string {
  return new Date(Date.parse(day) + offset * DAY_MS).toISOString().slice(0, DAY_TEXT_LENGTH)
}

export function formatClock(iso: string): string {
  const date = toLocalDate(iso)
  return date === undefined ? '' : date.toLocaleTimeString(CLOCK_LOCALE, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
}
