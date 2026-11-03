import { useConnection } from '@/hooks/useRelay'

export function useEditsDisabled(): boolean {
  return useConnection().kind !== 'ready'
}
