import { useQueryClient } from '@tanstack/react-query'
import { QUERY_KEYS } from '@/app/queryKeys'
import type { CommandError } from '@/features/relay/commands'
import type { ConfigPatch, ConfigSnapshot, ConfigUpdate, Saved } from '@/features/relay/contract/payload.gen'
import { useCommand } from '@/features/relay/useCommand'
import { applyConfigPatch } from '@/features/settings/configPatch'

type ConfigUpdateDraft = Omit<ConfigUpdate, 'id'>

interface PatchContext {
  previous: ConfigSnapshot
  written: ConfigSnapshot
}

interface ConfigPatchOptions {
  onSaved?: (saved: Saved) => void
  onFailed?: (error: CommandError) => void
}

export function useConfigPatch({ onSaved, onFailed }: ConfigPatchOptions = {}) {
  const queryClient = useQueryClient()
  const command = useCommand<ConfigUpdateDraft, PatchContext | undefined>({
    onMutate: ({ patch }): PatchContext | undefined => {
      const previous = queryClient.getQueryData<ConfigSnapshot>(QUERY_KEYS.config)
      if (previous === undefined) return undefined
      const written = queryClient.setQueryData<ConfigSnapshot>(QUERY_KEYS.config, applyConfigPatch(previous, patch))
      return written && { previous, written }
    },
    onSuccess: (saved) => onSaved?.(saved),
    onError: (error, _draft, context) => {
      if (context !== undefined && queryClient.getQueryData(QUERY_KEYS.config) === context.written) queryClient.setQueryData(QUERY_KEYS.config, context.previous)
      onFailed?.(error)
    },
  })
  return { patch: (patch: ConfigPatch) => command.mutate({ type: 'config.update', v: 1, patch }) }
}
