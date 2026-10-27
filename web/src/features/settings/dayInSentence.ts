import { formatDayInSentence } from '@/shared/format/dates'
import { en, type Messages } from '@/shared/i18n/en'

export function dayInSentence(iso: string, today: string, text: Messages = en): string {
  return formatDayInSentence(iso, today, text)
}
