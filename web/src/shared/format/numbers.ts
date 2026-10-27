import { en, type Messages } from '@/shared/i18n/en'

export function formatCount(count: number, text: Messages = en): string {
  return count.toLocaleString(text.numbers.locale)
}
