import type {
  ActiveAd,
  AdsSnapshot,
  ClosedAd,
  HiddenAd,
  HiddenKind,
  Mark,
  YearBand,
} from '@/features/relay/contract/payload.gen'
import { en, type Messages } from '@/shared/i18n/en'

export type JobList = 'today' | 'active' | 'hidden' | 'closed' | 'all'

export interface HiddenPart {
  kind: HiddenKind
  label: string
  count: number
}

export interface HiddenBreakdown {
  total: number
  parts: HiddenPart[]
}

const YEAR_BAND_CAPS: Record<YearBand, number | undefined> = {
  '0-1': 1,
  '1-3': 3,
  '3-6': 6,
  '6+': undefined,
}
const NO_YEARS = 0

const HIDDEN_KINDS: readonly HiddenKind[] = ['senior', 'years', 'masters', 'role', 'place', 'other']

function byDeadline(left: ActiveAd, right: ActiveAd): number {
  if (left.deadline !== right.deadline) {
    if (left.deadline === null) return 1
    if (right.deadline === null) return -1
    return left.deadline < right.deadline ? -1 : 1
  }
  return right.found_at.localeCompare(left.found_at)
}

export function selectToday(snapshot: AdsSnapshot): ActiveAd[] {
  return snapshot.active.filter((ad) => ad.today)
}

export function selectActive(snapshot: AdsSnapshot): ActiveAd[] {
  return snapshot.active.toSorted(byDeadline)
}

export function selectHidden(snapshot: AdsSnapshot, kind: HiddenKind | undefined): HiddenAd[] {
  return kind === undefined ? snapshot.hidden : snapshot.hidden.filter((ad) => ad.kind === kind)
}

export function selectClosed(snapshot: AdsSnapshot): ClosedAd[] {
  return snapshot.closed.toSorted((left, right) => right.closed_on.localeCompare(left.closed_on))
}

export function listCounts(snapshot: AdsSnapshot): Record<JobList, number> {
  return {
    today: selectToday(snapshot).length,
    active: snapshot.active_total,
    hidden: snapshot.hidden_total,
    closed: snapshot.closed_total,
    all: snapshot.active_total + snapshot.hidden_total + snapshot.closed_total,
  }
}

function maxYears(bands: readonly YearBand[]): number | undefined {
  if (bands.length === 0) return undefined
  let max = NO_YEARS
  for (const band of bands) {
    const cap = YEAR_BAND_CAPS[band]
    if (cap === undefined) return undefined
    max = Math.max(max, cap)
  }
  return max
}

function hiddenLabel(kind: HiddenKind, bands: readonly YearBand[] | undefined, text: Messages): string {
  const words = text.jobs.hidden
  if (kind !== 'years') return words.kinds[kind]
  const cap = bands === undefined ? undefined : maxYears(bands)
  return cap === undefined ? words.yearsUnknown : words.yearsAbove(cap)
}

export function hiddenBreakdown(snapshot: AdsSnapshot, bands: readonly YearBand[] | undefined, text: Messages = en): HiddenBreakdown {
  const parts = HIDDEN_KINDS.map((kind) => ({ kind, label: hiddenLabel(kind, bands, text), count: snapshot.hidden_counts[kind] }))
    .filter((part) => part.count > 0)
    .toSorted((left, right) => right.count - left.count)
  return { total: snapshot.hidden_total, parts }
}

export function markAd(snapshot: AdsSnapshot, adId: string, mark: Mark | null, markedAt: string | null): AdsSnapshot {
  return {
    ...snapshot,
    active: snapshot.active.map((ad) => (ad.id === adId ? { ...ad, mark, marked_at: markedAt } : ad)),
  }
}
