import { LEVEL_LABELS, LEVELS, YEAR_BAND_LABELS, YEAR_BANDS } from '@/features/jobs/labels'
import type { Targets } from '@/features/relay/contract/payload.gen'

export interface TargetsSummary {
  level: string
  years: string
  masters: string
  places: string
}

const ALL_PICKED = 'All'
const NONE_PICKED = 'None picked'
const OPEN_TO_MOVING = 'open to moving'

function pickedText<K extends string>(order: readonly K[], labels: Record<K, string>, picked: readonly K[]): string {
  const keys = order.filter((key) => picked.includes(key))
  if (keys.length === 0) return NONE_PICKED
  if (keys.length === order.length) return ALL_PICKED
  return keys.map((key) => labels[key]).join(', ')
}

function placesText(places: readonly string[], relocate: boolean): string {
  if (places.length === 0) return `Anywhere, ${OPEN_TO_MOVING}`
  return relocate ? `${places.join(', ')}, ${OPEN_TO_MOVING}` : places.join(', ')
}

export function summarizeTargets(targets: Targets): TargetsSummary {
  return {
    level: pickedText(LEVELS, LEVEL_LABELS, targets.levels),
    years: pickedText(YEAR_BANDS, YEAR_BAND_LABELS, targets.years),
    masters: targets.hide_masters ? 'Not required' : 'Allowed',
    places: placesText(targets.places, targets.relocate),
  }
}
