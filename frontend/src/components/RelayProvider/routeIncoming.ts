import type { QueryClient } from '@tanstack/react-query'
import { QUERY_KEYS } from '@/constants/queryKeys'
import type { CommandTracker } from '@/services/relayCommands/commandTracker'
import type { UnpairReason } from '@/services/relayConnection/connectionStore'
import type { Payload, Status } from '@/contract/payload.gen'
import { reduceRun, type RunView, settleRun } from '@/helpers/runProgressView'

export interface RouteDeps {
  commands: Pick<CommandTracker, 'settle'>
  onUnpaired(reason: Exclude<UnpairReason, 'ticket'>): void
}

const SNAPSHOT_KEYS: Partial<Record<Payload['type'], readonly [string]>> = {
  status: QUERY_KEYS.status,
  'ads.snapshot': QUERY_KEYS.ads,
  'applications.snapshot': QUERY_KEYS.applications,
  'config.snapshot': QUERY_KEYS.config,
  'profile.snapshot': QUERY_KEYS.profile,
  'usage.snapshot': QUERY_KEYS.usage,
  'models.snapshot': QUERY_KEYS.models,
  'guidance.snapshot': QUERY_KEYS.guidance,
  'context.snapshot': QUERY_KEYS.context,
  'learning.snapshot': QUERY_KEYS.learning,
}

function clearEndedRun(client: QueryClient, status: Status): void {
  const view = client.getQueryData<RunView>(QUERY_KEYS.run)
  if (view !== undefined && settleRun(view, status) === undefined) void client.resetQueries({ queryKey: QUERY_KEYS.run })
}

export function routeIncoming(message: Payload, client: QueryClient, deps: RouteDeps): void {
  const key = SNAPSHOT_KEYS[message.type]
  if (key) client.setQueryData(key, message)
  switch (message.type) {
    case 'status':
      clearEndedRun(client, message)
      return
    case 'run.progress':
    case 'run.finished':
    case 'run.cancelled':
      client.setQueryData<RunView>(QUERY_KEYS.run, (view) => reduceRun(view, message))
      return
    case 'saved':
    case 'error':
      deps.commands.settle(message)
      return
    case 'unpaired':
      deps.onUnpaired(message.reason === 'refused' ? 'refused' : 'revoked')
      return
    default:
      return
  }
}
