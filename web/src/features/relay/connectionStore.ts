import type { FailReason } from '@/features/relay/sessionRunner'

export type UnpairReason = 'refused' | 'revoked' | 'ticket'

export type ConnectionState =
  | { kind: 'loading' }
  | { kind: 'unpaired'; reason: UnpairReason | undefined }
  | { kind: 'connecting' }
  | { kind: 'handshaking' }
  | { kind: 'ready' }
  | { kind: 'machineOffline'; lastSeen: string | undefined }
  | { kind: 'relayDown'; retryAt: number }
  | { kind: 'networkOffline' }
  | { kind: 'failed'; reason: FailReason }

export interface ConnectionStore {
  getSnapshot(): ConnectionState
  subscribe(listener: () => void): () => void
  dispatch(next: ConnectionState): void
}

export function connectionReducer(state: ConnectionState, next: ConnectionState): ConnectionState {
  if (state.kind === 'unpaired') return state
  if (state.kind === 'failed') return next.kind === 'unpaired' ? next : state
  if (state.kind === 'machineOffline' && next.kind === 'handshaking') return state
  return next
}

export function createConnectionStore(): ConnectionStore {
  let state: ConnectionState = { kind: 'loading' }
  const listeners = new Set<() => void>()
  return {
    getSnapshot: () => state,
    subscribe: (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    dispatch: (next) => {
      const reduced = connectionReducer(state, next)
      if (reduced === state) return
      state = reduced
      for (const listener of listeners) listener()
    },
  }
}
