import { type QueryClient, skipToken, useQuery } from '@tanstack/react-query'
import { QUERY_KEYS } from '@/app/queryKeys'
import type {
  AdsSnapshot,
  ApplicationsSnapshot,
  ConfigSnapshot,
  ContextSnapshot,
  GuidanceSnapshot,
  ModelsSnapshot,
  ProfileSnapshot,
  Status,
  UsageSnapshot,
} from '@/features/relay/contract/payload.gen'
import type { RunView } from '@/features/run/runView'

export interface Snapshots {
  status: Status
  ads: AdsSnapshot
  applications: ApplicationsSnapshot
  config: ConfigSnapshot
  profile: ProfileSnapshot
  context: ContextSnapshot
  guidance: GuidanceSnapshot
  usage: UsageSnapshot
  models: ModelsSnapshot
  run: RunView
}

export type SnapshotData = Omit<Snapshots, 'run'>

export function seedSnapshots(client: QueryClient, data: Partial<Snapshots>): void {
  for (const [name, value] of Object.entries(data)) client.setQueryData(QUERY_KEYS[name as keyof Snapshots], value)
}

export function useSnapshot<Name extends keyof Snapshots>(name: Name): Snapshots[Name] | undefined {
  return useQuery<Snapshots[Name]>({ queryKey: QUERY_KEYS[name], queryFn: skipToken }).data
}
