import { afterAll, beforeAll, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Application, ApplicationsSnapshot } from '@/features/relay/contract/payload.gen'

export const TODAY = '2026-10-12'

export const applicationsSnapshot: ApplicationsSnapshot = payloadFixtures['applications.snapshot']

const [found, byHand] = applicationsSnapshot.applications
if (!found || !byHand) {
  throw new Error('applications.snapshot fixture must hold a found and a hand-added application')
}

export const foundApplication: Application = found
export const removedApplication: Application = byHand
export const handApplication: Application = { ...byHand, removed: false }

const TEST_TIME_ZONE = 'Europe/Oslo'

export function pinTimeZone() {
  beforeAll(() => {
    vi.stubEnv('TZ', TEST_TIME_ZONE)
  })
  afterAll(() => {
    vi.unstubAllEnvs()
  })
}
