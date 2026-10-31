import { LEVELS, YEAR_BANDS } from '@/shared/labels/targets'
import type { Targets } from '@/features/relay/contract/payload.gen'
import { en, type Messages } from '@/shared/i18n/en'

export interface TargetsSummary {
  level: string
  years: string
  masters: string
  places: string
}

const LIST_SEPARATOR = ', '

function pickedText<K extends string>(order: readonly K[], labels: Record<K, string>, picked: readonly K[], text: Messages): string {
  const keys = order.filter((key) => picked.includes(key))
  if (keys.length === 0) return text.jobs.config.none
  if (keys.length === order.length) return text.jobs.config.all
  return keys.map((key) => labels[key]).join(LIST_SEPARATOR)
}

function placesText(places: readonly string[], relocate: boolean, text: Messages): string {
  if (places.length === 0) return text.jobs.config.anywhere
  const listed = places.join(LIST_SEPARATOR)
  return relocate ? text.jobs.config.openToMoving(listed) : listed
}

export function summarizeTargets(targets: Targets, text: Messages = en): TargetsSummary {
  return {
    level: pickedText(LEVELS, text.targets.levels, targets.levels, text),
    years: pickedText(YEAR_BANDS, text.targets.years, targets.years, text),
    masters: targets.hide_masters ? text.jobs.config.notRequired : text.jobs.config.allowed,
    places: placesText(targets.places, targets.relocate, text),
  }
}
