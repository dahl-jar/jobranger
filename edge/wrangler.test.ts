import { unstable_readConfig } from 'wrangler'
import { describe, expect, it } from 'vitest'
import { LIMITERS } from './src/limiters.ts'

const PERIOD_SECONDS = 60

const EXPECTED_LIMITS = [
  { name: 'PAIR_CLIENT', limit: 6 },
  { name: 'PAIR_NETWORK', limit: 30 },
  { name: 'SESSION_CLIENT', limit: 30 },
  { name: 'SESSION_NETWORK', limit: 150 },
  { name: 'HUB_CLIENT', limit: 10 },
  { name: 'HUB_NETWORK', limit: 16 },
]

const config = unstable_readConfig({ config: 'wrangler.toml' })

describe('wrangler.toml', () => {
  it('should cap each limiter per minute', () => {
    const configured = config.ratelimits.map(({ name, simple }) => ({ name, limit: simple.limit, period: simple.period }))

    expect(configured).toEqual(EXPECTED_LIMITS.map((binding) => ({ ...binding, period: PERIOD_SECONDS })))
  })

  it('should bind every limiter the worker reads', () => {
    const bindings = Object.values(LIMITERS).flatMap(({ client, network }) => [client, network])

    expect(config.ratelimits.map(({ name }) => name).toSorted()).toEqual(bindings.toSorted())
  })

  it('should give each limiter its own counters', () => {
    const namespaces = new Set(config.ratelimits.map(({ namespace_id }) => namespace_id))

    expect(namespaces.size).toBe(config.ratelimits.length)
  })

  it('should run on every relay path', () => {
    expect({ routes: config.routes, workersDev: config.workers_dev }).toEqual({
      routes: [{ pattern: 'relay.jobranger.no/*', zone_name: 'jobranger.no' }],
      workersDev: false,
    })
  })

  it('should keep request logs off', () => {
    expect({
      invocationLogs: config.observability?.logs?.invocation_logs,
      traces: config.observability?.traces?.enabled,
    }).toEqual({ invocationLogs: false, traces: false })
  })
})
