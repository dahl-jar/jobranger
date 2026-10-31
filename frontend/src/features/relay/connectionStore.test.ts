import { describe, expect, it } from 'vitest'
import { connectionReducer, type ConnectionState, createConnectionStore } from '@/features/relay/connectionStore'

const OFFLINE: ConnectionState = { kind: 'machineOffline', lastSeen: '2026-10-13T14:02:00.000Z' }

describe('connectionReducer', () => {
  it.each<ConnectionState>([{ kind: 'unpaired', reason: 'ticket' }, { kind: 'failed', reason: 'cli_key' }])(
    'should ignore later events once $kind',
    (state) => {
      expect(connectionReducer(state, { kind: 'ready' })).toBe(state)
    },
  )

  it('should become unpaired after a failure once the user asks to pair again', () => {
    expect(connectionReducer({ kind: 'failed', reason: 'unknown_device' }, { kind: 'unpaired', reason: undefined })).toEqual({
      kind: 'unpaired',
      reason: undefined,
    })
  })

  it('should keep machineOffline while a new handshake runs', () => {
    expect(connectionReducer(OFFLINE, { kind: 'handshaking' })).toBe(OFFLINE)
  })

  it('should leave machineOffline once ready', () => {
    expect(connectionReducer(OFFLINE, { kind: 'ready' })).toEqual({ kind: 'ready' })
  })

  it('should take the next state from a live connection', () => {
    expect(connectionReducer({ kind: 'ready' }, OFFLINE)).toBe(OFFLINE)
  })
})

describe('createConnectionStore', () => {
  it('should start loading', () => {
    expect(createConnectionStore().getSnapshot()).toEqual({ kind: 'loading' })
  })

  it('should tell subscribers about each change', () => {
    const store = createConnectionStore()
    const seen: ConnectionState[] = []
    store.subscribe(() => seen.push(store.getSnapshot()))

    store.dispatch({ kind: 'connecting' })
    store.dispatch({ kind: 'handshaking' })

    expect(seen).toEqual([{ kind: 'connecting' }, { kind: 'handshaking' }])
  })

  it('should not tell subscribers when the state stays the same', () => {
    const store = createConnectionStore()
    store.dispatch({ kind: 'failed', reason: 'handshake' })
    let calls = 0
    store.subscribe(() => {
      calls += 1
    })

    store.dispatch({ kind: 'ready' })

    expect(calls).toBe(0)
  })

  it('should stop telling a subscriber after it unsubscribes', () => {
    const store = createConnectionStore()
    let calls = 0
    const unsubscribe = store.subscribe(() => {
      calls += 1
    })

    unsubscribe()
    store.dispatch({ kind: 'connecting' })

    expect(calls).toBe(0)
  })
})
