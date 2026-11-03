import { type QueryKey, useQueryClient } from '@tanstack/react-query'
import type { CommandDraft, CommandError } from '@/services/relayCommands/commandTracker'
import type { Saved } from '@/contract/payload.gen'
import { useCommand } from '@/hooks/useCommand'

interface InstantSaveOptions<Data, Draft extends CommandDraft> {
  queryKey: QueryKey
  apply: (data: Data, draft: Draft) => Data
  holds: (current: Data, draft: Draft, written: Data) => boolean
  restore: (current: Data, draft: Draft, previous: Data) => Data
  onError?: ((error: CommandError, draft: Draft) => void) | undefined
  onSuccess?: ((saved: Saved, draft: Draft) => void) | undefined
}

interface WriteContext<Data> {
  previous: Data
  written: Data
}

export function useInstantSave<Data extends object, Draft extends CommandDraft>({ queryKey, apply, holds, restore, onError, onSuccess }: InstantSaveOptions<Data, Draft>) {
  const queryClient = useQueryClient()
  return useCommand<Draft, WriteContext<Data> | undefined>({
    onMutate: (draft) => {
      const previous = queryClient.getQueryData<Data>(queryKey)
      if (previous === undefined) return undefined
      const written = queryClient.setQueryData<Data>(queryKey, apply(previous, draft))
      return written && { previous, written }
    },
    onSuccess: (saved, draft) => onSuccess?.(saved, draft),
    onError: (error, draft, context) => {
      const current = queryClient.getQueryData<Data>(queryKey)
      if (context !== undefined && current !== undefined && holds(current, draft, context.written)) {
        queryClient.setQueryData<Data>(queryKey, restore(current, draft, context.previous))
      }
      onError?.(error, draft)
    },
  })
}
