import { payloadFixtures } from '@/contract/fixtures.gen'
import type { UsageDay } from '@/contract/payload.gen'

export const usage = payloadFixtures['usage.snapshot']
export const [yesterday, today] = usage.days as [UsageDay, UsageDay]
export const pricedDays = [{ ...yesterday, cost_usd: 0.2 }, { ...today, cost_usd: 0.4 }]
