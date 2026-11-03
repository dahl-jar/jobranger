import { createContext, useContext, useSyncExternalStore } from 'react'
import type { CommandDraft } from '@/services/relayCommands/commandTracker'
import type { ConnectionState, ConnectionStore } from '@/services/relayConnection/connectionStore'
import type { Saved } from '@/contract/payload.gen'
import type { RelayClientDeps } from '@/services/relaySession/relayClient'
import type { SendProgress } from '@/services/relaySession/sessionRunner'

export type RelayDeps = Omit<RelayClientDeps, 'onState' | 'onMessage' | 'onDisconnect' | 'onSessionReset' | 'onReady'>

export interface Relay {
  store: ConnectionStore
  send(draft: CommandDraft, onProgress?: SendProgress): Promise<Saved>
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
