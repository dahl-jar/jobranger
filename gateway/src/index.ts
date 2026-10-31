import { LIMITERS, TIERS, type LimiterName, type Tier } from './limiters.ts'
import { logLimiterFailure, logRefusal } from './log.ts'
import { clientKeys, type ClientKeys } from './network.ts'
import { notFound, rateLimited } from './responses.ts'
import { groupFor, type RouteGroup } from './routes.ts'

export type Env = Readonly<Record<LimiterName, RateLimit>> & {
  readonly SITE_ORIGIN: string
}

const PREFLIGHT = 'OPTIONS'
const CLIENT_ADDRESS = 'CF-Connecting-IP'
const ORIGIN_ADDRESS = 'X-Real-IP'
const FORWARDED_FOR = 'X-Forwarded-For'

function toOrigin(request: Request, address: string | null): Request {
  const headers = new Headers(request.headers)
  headers.delete(FORWARDED_FOR)
  if (address === null) {
    headers.delete(ORIGIN_ADDRESS)
  } else {
    headers.set(ORIGIN_ADDRESS, address)
  }
  return new Request(request, { headers })
}

async function refusingTier(env: Env, group: RouteGroup, keys: ClientKeys, scope: string): Promise<Tier | undefined> {
  for (const tier of TIERS) {
    try {
      const { success } = await env[LIMITERS[group][tier]].limit({ key: `${scope}:${keys[tier]}` })
      if (!success) {
        return tier
      }
    } catch (error: unknown) {
      logLimiterFailure(group, tier, error)
    }
  }
  return undefined
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const group = groupFor(request.url)
    if (group === undefined) {
      return notFound()
    }

    const address = request.headers.get(CLIENT_ADDRESS)
    const keys = clientKeys(address)
    const scope = request.method === PREFLIGHT ? 'preflight' : 'request'
    const tier = await refusingTier(env, group, keys, scope)
    if (tier !== undefined) {
      logRefusal(group, tier)
      return rateLimited(request, env.SITE_ORIGIN)
    }

    return fetch(toOrigin(request, address))
  },
} satisfies ExportedHandler<Env>
