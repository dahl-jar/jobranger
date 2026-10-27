import { describe, expect, it } from 'vitest'
import { pairingReducer } from '@/features/pairing/pairingMachine'
import type { CloseReason } from '@/features/relay/sessionRunner'

describe('pairingReducer', () => {
  it.each([
    { reason: 'abort', ended: 'refused' },
    { reason: 'unknown_session', ended: 'refused' },
    { reason: 'timeout', ended: 'silent' },
    { reason: 'no_answer', ended: 'silent' },
    { reason: 'words_timeout', ended: 'timeout' },
    { reason: 'rejected', ended: 'cancelled' },
    { reason: 'connection', ended: 'dropped' },
    { reason: 'offline', ended: 'dropped' },
    { reason: 'broken', ended: 'failed' },
  ] satisfies { reason: CloseReason; ended: string }[])('should end a pairing closed by $reason as $ended', ({ reason, ended }) => {
    const state = pairingReducer({ name: 'waiting' }, { type: 'session', event: { kind: 'closed', reason } })

    expect(state).toEqual({ name: 'form', ended })
  })

  it('should stay connected when the session closes after pairing', () => {
    const state = pairingReducer({ name: 'connected' }, { type: 'session', event: { kind: 'closed', reason: 'connection' } })

    expect(state).toEqual({ name: 'connected' })
  })

  it('should ignore Yes outside the word compare', () => {
    expect(pairingReducer({ name: 'handshaking' }, { type: 'answered' })).toEqual({ name: 'handshaking' })
  })
})
