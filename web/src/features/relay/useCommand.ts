import { useMutation, type UseMutationOptions } from '@tanstack/react-query'
import type { CommandDraft, CommandError } from '@/features/relay/commands'
import type { Saved } from '@/features/relay/contract/payload.gen'
import { useConnection, useRelay } from '@/features/relay/relayContext'

type CommandOptions<Draft extends CommandDraft, Context> = Omit<UseMutationOptions<Saved, CommandError, Draft, Context>, 'mutationFn'>

export function useCommand<Draft extends CommandDraft, Context = unknown>(options: CommandOptions<Draft, Context> = {}) {
  const relay = useRelay()
  const disabled = useConnection().kind !== 'ready'
  const mutation = useMutation({ ...options, mutationFn: (draft: Draft) => relay.send(draft) })
  return { ...mutation, disabled }
}
