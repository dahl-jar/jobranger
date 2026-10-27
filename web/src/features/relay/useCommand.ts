import { useMutation, type UseMutationOptions } from '@tanstack/react-query'
import { useState } from 'react'
import type { CommandDraft, CommandError } from '@/features/relay/commands'
import type { Saved } from '@/features/relay/contract/payload.gen'
import { useConnection, useRelay } from '@/features/relay/relayContext'

type CommandOptions<Draft extends CommandDraft, Context> = Omit<UseMutationOptions<Saved, CommandError, Draft, Context>, 'mutationFn'>

const NO_PROGRESS = 0

export function useCommand<Draft extends CommandDraft, Context = unknown>(options: CommandOptions<Draft, Context> = {}) {
  const relay = useRelay()
  const disabled = useConnection().kind !== 'ready'
  const [progress, setProgress] = useState(NO_PROGRESS)
  const mutation = useMutation({
    ...options,
    mutationFn: (draft: Draft) => {
      setProgress(NO_PROGRESS)
      return relay.send(draft, (sent, count) => setProgress(sent / count))
    },
  })
  return { ...mutation, disabled, progress: mutation.isPending ? progress : undefined }
}
