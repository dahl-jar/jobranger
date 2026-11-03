import type { Messages } from '@/i18n/en'

export type SearchField = 'roles' | 'levels' | 'years' | 'hideMasters' | 'places'

const NEXT_RUN_FIELDS: ReadonlySet<SearchField> = new Set(['levels', 'years', 'hideMasters'])

export function searchTimingLabel(field: SearchField, text: Messages): string {
  return NEXT_RUN_FIELDS.has(field) ? text.settings.search.nextRun : text.settings.search.now
}
