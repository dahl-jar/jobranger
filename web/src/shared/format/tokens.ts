import { en, type Messages } from '@/shared/i18n/en'

const THOUSAND = 1_000
const MILLION = 1_000_000
const BILLION = 1_000_000_000
const TENTHS = 10
const DECIMAL_POINT = '.'

function inTenths(count: number, unit: number): number {
  return Math.round((count * TENTHS) / unit) / TENTHS
}

function withDecimal(value: number, text: Messages): string {
  return String(value).replace(DECIMAL_POINT, text.numbers.decimal)
}

export function formatTokens(count: number, text: Messages = en): string {
  if (!Number.isFinite(count)) {
    return '0'
  }
  if (count < THOUSAND) {
    return String(count)
  }
  const thousands = Math.round(count / THOUSAND)
  if (thousands < THOUSAND) {
    return `${thousands}${text.numbers.thousand}`
  }
  const millions = inTenths(count, MILLION)
  if (millions < THOUSAND) {
    return `${withDecimal(millions, text)}${text.numbers.million}`
  }
  return `${withDecimal(inTenths(count, BILLION), text)}${text.numbers.billion}`
}
