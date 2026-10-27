import { describe, expect, it } from 'vitest'
import {
  ADS_SNAPSHOT,
  APPLIED_AD,
  CLOSED_AD,
  DATED_AD,
  OTHER_HIDDEN_AD,
  TRUNCATED_SNAPSHOT,
  YEARS_HIDDEN_AD,
} from '@/features/jobs/goldenPayloads'
import {
  hiddenBreakdown,
  listCounts,
  markAd,
  selectActive,
  selectClosed,
  selectHidden,
  selectToday,
} from '@/features/jobs/selectors'

const ids = (ads: readonly { id: string }[]) => ads.map((ad) => ad.id)

describe('selectToday', () => {
  it('should keep only ads found today', () => {
    const snapshot = { ...ADS_SNAPSHOT, active: [{ ...DATED_AD, today: false }, APPLIED_AD] }

    expect(ids(selectToday(snapshot))).toEqual([APPLIED_AD.id])
  })
})

describe('selectActive', () => {
  const early = { ...DATED_AD, id: 'early', deadline: '2026-10-14' }

  it.each([
    [[APPLIED_AD, DATED_AD, early]],
    [[early, APPLIED_AD, DATED_AD]],
    [[DATED_AD, early, APPLIED_AD]],
    [[early, DATED_AD, APPLIED_AD]],
  ])('should sort by deadline with undated ads last %#', (active) => {
    expect(ids(selectActive({ ...ADS_SNAPSHOT, active }))).toEqual(['early', DATED_AD.id, APPLIED_AD.id])
  })

  it('should put the newest find first when deadlines tie', () => {
    const newer = { ...DATED_AD, id: 'newer', found_at: '2026-10-11T03:00:00Z' }
    const snapshot = { ...ADS_SNAPSHOT, active: [DATED_AD, newer] }

    expect(ids(selectActive(snapshot))).toEqual(['newer', DATED_AD.id])
  })
})

describe('selectHidden', () => {
  it('should keep only ads hidden for the picked reason', () => {
    expect(ids(selectHidden(ADS_SNAPSHOT, 'other'))).toEqual([OTHER_HIDDEN_AD.id])
  })

  it('should keep every hidden ad when no reason is picked', () => {
    expect(ids(selectHidden(ADS_SNAPSHOT, undefined))).toEqual([YEARS_HIDDEN_AD.id, OTHER_HIDDEN_AD.id])
  })
})

describe('selectClosed', () => {
  it('should put the latest closed ad first', () => {
    const later = { ...CLOSED_AD, id: 'later', closed_on: '2026-10-10' }
    const snapshot = { ...ADS_SNAPSHOT, closed: [CLOSED_AD, later] }

    expect(ids(selectClosed(snapshot))).toEqual(['later', CLOSED_AD.id])
  })
})

describe('listCounts', () => {
  it('should count Today from the list', () => {
    const snapshot = { ...TRUNCATED_SNAPSHOT, active: [{ ...DATED_AD, today: false }, APPLIED_AD] }

    expect(listCounts(snapshot)).toEqual({ today: 1, active: 120, hidden: 41, closed: 9, all: 170 })
  })
})

describe('hiddenBreakdown', () => {
  const COUNTS = { senior: 18, years: 12, masters: 6, role: 0, place: 0, other: 5 }
  const NO_COUNTS = { senior: 0, years: 0, masters: 0, role: 0, place: 0, other: 0 }

  it('should list reason counts biggest first', () => {
    const snapshot = { ...ADS_SNAPSHOT, hidden_total: 48, hidden_counts: { ...COUNTS, years: 19 } }

    expect(hiddenBreakdown(snapshot, ['0-1', '1-3'])).toEqual({
      total: 48,
      parts: [
        { kind: 'years', label: 'need 3+ yrs', count: 19 },
        { kind: 'senior', label: 'senior', count: 18 },
        { kind: 'masters', label: "master's", count: 6 },
        { kind: 'other', label: 'other reasons', count: 5 },
      ],
    })
  })

  it('should keep the contract order for reasons with the same count', () => {
    const snapshot = {
      ...ADS_SNAPSHOT,
      hidden_total: 5,
      hidden_counts: { senior: 2, years: 0, masters: 0, role: 1, place: 2, other: 0 },
    }

    expect(hiddenBreakdown(snapshot, undefined).parts.map((part) => part.kind)).toEqual(['senior', 'place', 'role'])
  })

  it.each([
    [['0-1'], 'need 1+ yrs'],
    [['1-3', '0-1'], 'need 3+ yrs'],
    [[], 'need more yrs'],
    [['1-3', '6+'], 'need more yrs'],
    [undefined, 'need more yrs'],
  ] as const)('should label the years reason from the year bands %j', (bands, label) => {
    expect(hiddenBreakdown(ADS_SNAPSHOT, bands).parts[0]?.label).toBe(label)
  })

  it.each([
    ['role', 'other roles'],
    ['place', 'other places'],
  ] as const)('should label the %s reason as %s', (kind, label) => {
    const snapshot = { ...ADS_SNAPSHOT, hidden_counts: { ...NO_COUNTS, [kind]: 1 } }

    expect(hiddenBreakdown(snapshot, undefined).parts[0]?.label).toBe(label)
  })
})

describe('markAd', () => {
  it('should set the mark and its time on the ad', () => {
    const next = markAd(ADS_SNAPSHOT, DATED_AD.id, 'skipped', '2026-10-12T08:00:00Z')

    expect(next.active[0]).toEqual({ ...DATED_AD, mark: 'skipped', marked_at: '2026-10-12T08:00:00Z' })
  })

  it('should leave other ads untouched', () => {
    const next = markAd(ADS_SNAPSHOT, DATED_AD.id, 'skipped', '2026-10-12T08:00:00Z')

    expect(next.active[1]).toBe(APPLIED_AD)
  })
})
