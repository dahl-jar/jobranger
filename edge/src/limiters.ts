import type { RouteGroup } from './routes.ts'

export const TIERS = ['client', 'network'] as const

export type Tier = (typeof TIERS)[number]

export const LIMITERS = {
  pair: { client: 'PAIR_CLIENT', network: 'PAIR_NETWORK' },
  session: { client: 'SESSION_CLIENT', network: 'SESSION_NETWORK' },
  hub: { client: 'HUB_CLIENT', network: 'HUB_NETWORK' },
} as const satisfies Record<RouteGroup, Record<Tier, string>>

export type LimiterName = (typeof LIMITERS)[RouteGroup][Tier]
