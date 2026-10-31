import type { Tier } from './limiters.ts'
import type { RouteGroup } from './routes.ts'

export function logRefusal(group: RouteGroup, tier: Tier): void {
  console.warn(JSON.stringify({ message: 'edge rate limit refused request', group, tier }))
}

export function logLimiterFailure(group: RouteGroup, tier: Tier, error: unknown): void {
  const reason = error instanceof Error ? error.name : typeof error
  console.error(JSON.stringify({ message: 'edge rate limit check failed, request passed', group, tier, reason }))
}
