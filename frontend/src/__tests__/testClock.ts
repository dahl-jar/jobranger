import { afterAll, beforeAll, vi } from 'vitest'

export const TODAY = '2026-10-12'

export const TEST_TIME_ZONE = 'Europe/Oslo'

export function pinTimeZone() {
  beforeAll(() => {
    vi.stubEnv('TZ', TEST_TIME_ZONE)
  })
  afterAll(() => {
    vi.unstubAllEnvs()
  })
}
