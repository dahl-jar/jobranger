import type { ClaimResult } from '@/features/pairing/claimPairing'
import type { PairCodeError } from '@/features/pairing/pairCode'
import type { Status } from '@/features/relay/contract/payload.gen'
import type { UnpairReason } from '@/features/relay/connectionStore'
import type { CloseReason, SessionEvent } from '@/features/relay/sessionRunner'

export type ClaimError = Exclude<ClaimResult['kind'], 'ok'>
export type FormError = PairCodeError | ClaimError | 'storage'
export type EndReason = 'refused' | 'timeout' | 'silent' | 'cancelled' | 'dropped' | 'failed' | 'revoked' | 'ticket'

export type PairingState =
  | { name: 'loading' }
  | { name: 'form'; error?: FormError; ended?: EndReason }
  | { name: 'claiming' }
  | { name: 'handshaking' }
  | { name: 'confirming'; words: string[] }
  | { name: 'waiting' }
  | { name: 'connected'; status?: Status }
  | { name: 'paired' }

export type PairingEvent =
  | { type: 'loaded'; paired: boolean; ended: EndReason | undefined }
  | { type: 'submitted' }
  | { type: 'rejected'; error: FormError }
  | { type: 'claimed' }
  | { type: 'answered' }
  | { type: 'session'; event: SessionEvent }
  | { type: 'unpaired' }
  | { type: 'revoked' }
  | { type: 'sessionEnded'; reason: UnpairReason | undefined }

const END_REASONS: Record<CloseReason, EndReason> = {
  abort: 'refused',
  unknown_session: 'refused',
  timeout: 'silent',
  no_answer: 'silent',
  words_timeout: 'timeout',
  rejected: 'cancelled',
  connection: 'dropped',
  offline: 'dropped',
  broken: 'failed',
}

function onSession(state: PairingState, event: SessionEvent): PairingState {
  if (state.name === 'connected') {
    return event.kind === 'message' && event.payload.type === 'status' ? { name: 'connected', status: event.payload } : state
  }
  switch (event.kind) {
    case 'handshaking':
      return { name: 'handshaking' }
    case 'confirming':
      return { name: 'confirming', words: event.words }
    case 'ready':
      return { name: 'connected' }
    case 'closed':
      return { name: 'form', ended: END_REASONS[event.reason] }
    case 'failed':
      return { name: 'form', ended: 'failed' }
    default:
      return state
  }
}

type FixedEvent = Extract<PairingEvent, { type: 'submitted' | 'claimed' | 'unpaired' | 'revoked' }>['type']

const FIXED_STATES: Record<FixedEvent, PairingState> = {
  submitted: { name: 'claiming' },
  claimed: { name: 'handshaking' },
  unpaired: { name: 'form' },
  revoked: { name: 'form', ended: 'revoked' },
}

function isFixed(event: PairingEvent): event is Extract<PairingEvent, { type: FixedEvent }> {
  return Object.hasOwn(FIXED_STATES, event.type)
}

function formAfter(ended: EndReason | undefined): PairingState {
  return ended === undefined ? { name: 'form' } : { name: 'form', ended }
}

export function pairingReducer(state: PairingState, event: PairingEvent): PairingState {
  if (isFixed(event)) return FIXED_STATES[event.type]
  switch (event.type) {
    case 'loaded':
      return event.paired ? { name: 'paired' } : formAfter(event.ended)
    case 'rejected':
      return { name: 'form', error: event.error }
    case 'answered':
      return state.name === 'confirming' ? { name: 'waiting' } : state
    case 'session':
      return onSession(state, event.event)
    case 'sessionEnded':
      return formAfter(event.reason)
  }
}
