import { en, type Messages } from '@/shared/i18n/en'

const SMALLEST_SHOWN = 0.01
const CENT_DIGITS = 2
const DOLLAR = '$'
const MINUS = '-'
const BELOW = '<'

function formatAmount(value: number, text: Messages): string {
  const amount = new Intl.NumberFormat(text.numbers.locale, { minimumFractionDigits: CENT_DIGITS, maximumFractionDigits: CENT_DIGITS }).format(Math.abs(value))
  return `${value < 0 ? MINUS : ''}${DOLLAR}${amount}`
}

export function formatUsd(value: number, text: Messages = en): string {
  if (!Number.isFinite(value)) {
    return formatAmount(0, text)
  }
  if (value > 0 && value < SMALLEST_SHOWN) {
    return `${BELOW}${formatAmount(SMALLEST_SHOWN, text)}`
  }
  return formatAmount(value, text)
}
