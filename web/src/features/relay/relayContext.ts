import { createContext, useContext, useSyncExternalStore } from 'react'
import type { CommandDraft } from '@/features/relay/commands'
import type { ConnectionState, ConnectionStore } from '@/features/relay/connectionStore'
import type { Saved } from '@/features/relay/contract/payload.gen'
import type { RelayClientDeps } from '@/features/relay/relayClient'

export type RelayDeps = Omit<RelayClientDeps, 'onState' | 'onMessage' | 'onDisconnect' | 'onSessionReset' | 'onReady'>

export interface Relay {
  store: ConnectionStore
  send(draft: CommandDraft): Promise<Saved>
  unpair(): Promise<void>
  retryNow(): void
}

export const RelayContext = createContext<Relay | undefined>(undefined)
export const RelayDepsContext = createContext<RelayDeps | undefined>(undefined)

export function useRelay(): Relay {
  const relay = useContext(RelayContext)
  if (!relay) throw new Error('useRelay needs a RelayProvider above it')
  return relay
}

export function useConnection(): ConnectionState {
  const { store } = useRelay()
  return useSyncExternalStore(store.subscribe, store.getSnapshot)
}
