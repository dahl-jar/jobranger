import { useConnection } from '@/features/relay/relayContext'

export function useEditsDisabled(): boolean {
  return useConnection().kind !== 'ready'
}
