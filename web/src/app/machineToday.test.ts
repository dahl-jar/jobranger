import { describe, expect, it } from 'vitest'
import { machineToday } from '@/app/machineToday'
import { SNAPSHOTS } from '@/test/appData'

const LATER_DAY = '2026-10-13'

describe('machineToday', () => {
  it('should take a usage day newer than the ads snapshot', () => {
    const usage = { ...SNAPSHOTS.usage, days: [...SNAPSHOTS.usage.days, { day: LATER_DAY, calls: 1, input_tokens: 1, output_tokens: 1, cost_usd: 0, ads_read: 1, ads_checked: 1 }] }

    expect(machineToday(SNAPSHOTS.ads, usage)).toBe(LATER_DAY)
  })

  it('should keep the ads day when no usage is newer', () => {
    expect(machineToday({ ...SNAPSHOTS.ads, as_of: LATER_DAY }, SNAPSHOTS.usage)).toBe(LATER_DAY)
  })
})
