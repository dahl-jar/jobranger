import type { AdsSnapshot, ConfigSnapshot, Payload } from '@/features/relay/contract/payload.gen'
import { parsePayload } from '@/features/relay/contract/validate'

const golden = import.meta.glob<unknown>(
  ['@messages/fixtures/payload/ads.snapshot.json', '@messages/fixtures/payload/config.snapshot.json'],
  { eager: true, import: 'default' },
)

function load(name: string): Payload {
  const entry = Object.entries(golden).find(([path]) => path.endsWith(`/${name}.json`))
  if (!entry) {
    throw new Error(`golden payload ${name}.json not found under messages/fixtures/payload`)
  }
  return parsePayload(JSON.stringify(entry[1]))
}

function loadAdsSnapshot(): AdsSnapshot {
  const payload = load('ads.snapshot')
  if (payload.type !== 'ads.snapshot') {
    throw new Error(`ads.snapshot.json holds a ${payload.type} payload`)
  }
  return payload
}

function loadConfigSnapshot(): ConfigSnapshot {
  const payload = load('config.snapshot')
  if (payload.type !== 'config.snapshot') {
    throw new Error(`config.snapshot.json holds a ${payload.type} payload`)
  }
  return payload
}

const REALISTIC_IDS = ['finn:401234567', 'teamtailor:org/1']

function withRealisticIds(snapshot: AdsSnapshot): AdsSnapshot {
  return {
    ...snapshot,
    active: snapshot.active.map((ad, index) => ({ ...ad, id: REALISTIC_IDS[index] ?? ad.id })),
  }
}

export const ADS_SNAPSHOT = withRealisticIds(loadAdsSnapshot())
export const TRUNCATED_SNAPSHOT: AdsSnapshot = {
  ...ADS_SNAPSHOT,
  active_total: 120,
  hidden_total: 41,
  hidden_counts: { senior: 12, years: 9, masters: 3, role: 8, place: 4, other: 5 },
  closed_total: 9,
}
export const CONFIG_SNAPSHOT = loadConfigSnapshot()

function at<T>(items: readonly T[], index: number): T {
  const item = items[index]
  if (item === undefined) {
    throw new Error(`golden payload has no item at index ${index}`)
  }
  return item
}

export const DATED_AD = at(ADS_SNAPSHOT.active, 0)
export const APPLIED_AD = at(ADS_SNAPSHOT.active, 1)
export const YEARS_HIDDEN_AD = at(ADS_SNAPSHOT.hidden, 0)
export const OTHER_HIDDEN_AD = at(ADS_SNAPSHOT.hidden, 1)
export const CLOSED_AD = at(ADS_SNAPSHOT.closed, 0)
