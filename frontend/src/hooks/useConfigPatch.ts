import { QUERY_KEYS } from '@/constants/queryKeys'
import { useInstantSave } from '@/hooks/useInstantSave'
import type { CommandError } from '@/services/relayCommands/commandTracker'
import type { ConfigPatch, ConfigSnapshot, ConfigUpdate, Saved } from '@/contract/payload.gen'
import { applyConfigPatch } from '@/helpers/applyConfigPatch'

type ConfigUpdateDraft = Omit<ConfigUpdate, 'id'>

interface ConfigPatchOptions {
  onSaved?: (saved: Saved) => void
  onFailed?: (error: CommandError) => void
}

export function useConfigPatch({ onSaved, onFailed }: ConfigPatchOptions = {}) {
  const command = useInstantSave<ConfigSnapshot, ConfigUpdateDraft>({
    queryKey: QUERY_KEYS.config,
    apply: (config, { patch }) => applyConfigPatch(config, patch),
    holds: (current, _draft, written) => current === written,
    restore: (_current, _draft, previous) => previous,
    onSuccess: onSaved,
    onError: onFailed,
  })
  return { patch: (patch: ConfigPatch) => command.mutate({ type: 'config.update', v: 1, patch }) }
}
