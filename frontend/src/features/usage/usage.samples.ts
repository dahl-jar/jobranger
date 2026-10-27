import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { UsageDay } from '@/features/relay/contract/payload.gen'

export const usage = payloadFixtures['usage.snapshot']
export const TODAY = '2026-10-12'
export const [yesterday, today] = usage.days as [UsageDay, UsageDay]
export const pricedDays = [{ ...yesterday, cost_usd: 0.2 }, { ...today, cost_usd: 0.4 }]
