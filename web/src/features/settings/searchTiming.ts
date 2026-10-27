export type SearchField = 'roles' | 'levels' | 'years' | 'hideMasters' | 'places'

const NEXT_RUN_FIELDS: ReadonlySet<SearchField> = new Set(['levels', 'years', 'hideMasters'])

function appliesFromNextRun(field: SearchField): boolean {
  return NEXT_RUN_FIELDS.has(field)
}

export function timingLabel(field: SearchField): string {
  return appliesFromNextRun(field) ? 'Applies from the next run.' : 'Applies now.'
}
