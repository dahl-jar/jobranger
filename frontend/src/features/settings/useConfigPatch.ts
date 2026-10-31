import { QUERY_KEYS } from '@/app/queryKeys'
import { useOptimisticWrite } from '@/app/useOptimisticWrite'
import type { CommandError } from '@/features/relay/commands'
import type { ConfigPatch, ConfigSnapshot, ConfigUpdate, Saved } from '@/features/relay/contract/payload.gen'
import { applyConfigPatch } from '@/features/settings/configPatch'

type ConfigUpdateDraft = Omit<ConfigUpdate, 'id'>

interface ConfigPatchOptions {
  onSaved?: (saved: Saved) => void
  onFailed?: (error: CommandError) => void
}

export function useConfigPatch({ onSaved, onFailed }: ConfigPatchOptions = {}) {
  const command = useOptimisticWrite<ConfigSnapshot, ConfigUpdateDraft>({
    queryKey: QUERY_KEYS.config,
    apply: (config, { patch }) => applyConfigPatch(config, patch),
    holds: (current, _draft, written) => current === written,
    restore: (_current, _draft, previous) => previous,
    onSuccess: onSaved,
    onError: onFailed,
  })
  return { patch: (patch: ConfigPatch) => command.mutate({ type: 'config.update', v: 1, patch }) }
}
