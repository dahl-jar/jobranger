import { describe, expect, it } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { QUERY_KEYS } from '@/app/queryKeys'
import type { ErrorMessage, Payload, RunCancelled, RunFinished, RunProgress, Saved, Status, Unpaired } from '@/features/relay/contract/payload.gen'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { routeIncoming, type RouteDeps } from '@/features/relay/messageRouter'

const STATUS = payloadFixtures.status as Status
const IDLE = payloadFixtures['status.idle'] as Status
const PROGRESS = payloadFixtures['run.progress'] as RunProgress
const FINISHED = payloadFixtures['run.finished'] as RunFinished
const CANCELLED = payloadFixtures['run.cancelled'] as RunCancelled

function setup() {
  const client = createQueryClient()
  const settled: (Saved | ErrorMessage)[] = []
  const unpaired: string[] = []
  const deps: RouteDeps = {
    commands: { settle: (reply) => void settled.push(reply) },
    onUnpaired: (reason) => void unpaired.push(reason),
  }
  const route = (message: Payload) => routeIncoming(message, client, deps)
  return { client, settled, unpaired, route }
}

describe('routeIncoming', () => {
  it.each([
    ['status', QUERY_KEYS.status],
    ['ads.snapshot', QUERY_KEYS.ads],
    ['applications.snapshot', QUERY_KEYS.applications],
    ['config.snapshot', QUERY_KEYS.config],
    ['profile.snapshot', QUERY_KEYS.profile],
    ['usage.snapshot', QUERY_KEYS.usage],
    ['models.snapshot', QUERY_KEYS.models],
    ['guidance.snapshot', QUERY_KEYS.guidance],
    ['context.snapshot', QUERY_KEYS.context],
    ['learning.snapshot', QUERY_KEYS.learning],
  ] as const)('should write %s into its query', (type, key) => {
    const { client, route } = setup()
    const message = payloadFixtures[type] as Payload

    route(message)

    expect(client.getQueryData(key)).toEqual(message)
  })

  it.each([
    ['run.progress', PROGRESS, { kind: 'running', progress: PROGRESS }],
    ['run.finished', FINISHED, { kind: 'finished', result: FINISHED }],
    ['run.cancelled', CANCELLED, { kind: 'cancelled', runId: CANCELLED.run_id }],
  ] as const)('should show %s in the run view', (_, message, view) => {
    const { client, route } = setup()

    route(message)

    expect(client.getQueryData(QUERY_KEYS.run)).toEqual(view)
  })

  it('should keep the run view when status still names the run', () => {
    const { client, route } = setup()
    route(PROGRESS)

    route(STATUS)

    expect(client.getQueryData(QUERY_KEYS.run)).toEqual({ kind: 'running', progress: PROGRESS })
  })

  it('should clear only the run view when status settles it away', () => {
    const { client, route } = setup()
    route(payloadFixtures['ads.snapshot'] as Payload)
    route(PROGRESS)

    route(IDLE)

    expect([client.getQueryData(QUERY_KEYS.run), client.getQueryData(QUERY_KEYS.ads)]).toEqual([undefined, payloadFixtures['ads.snapshot']])
  })

  it.each(['saved', 'error'] as const)('should settle the waiting command with %s', (type) => {
    const { settled, route } = setup()
    const reply = payloadFixtures[type] as Payload

    route(reply)

    expect(settled).toEqual([reply])
  })

  it.each([
    ['refused', 'refused'],
    ['revoked', 'cli'],
  ] as const)('should unpair this browser as %s when the machine says %s', (expected, reason) => {
    const { unpaired, route } = setup()

    route({ ...(payloadFixtures.unpaired as Unpaired), reason })

    expect(unpaired).toEqual([expected])
  })

  it('should ignore a message the browser never receives', () => {
    const { client, settled, unpaired, route } = setup()

    route(payloadFixtures['run.start'] as Payload)

    expect([client.getQueryCache().getAll(), settled, unpaired]).toEqual([[], [], []])
  })
})
