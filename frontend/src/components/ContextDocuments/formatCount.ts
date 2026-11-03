import { en, type Messages } from '@/i18n/en'

export function formatCount(count: number, text: Messages = en): string {
  return count.toLocaleString(text.numbers.locale)
}
