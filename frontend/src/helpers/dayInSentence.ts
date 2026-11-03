import { formatDayInSentence } from '@/helpers/formatDate'
import { en, type Messages } from '@/i18n/en'

export function dayInSentence(iso: string, today: string, text: Messages = en): string {
  return formatDayInSentence(iso, today, text)
}
