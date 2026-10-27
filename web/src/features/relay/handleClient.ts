import { CONTRACT_VERSION } from '@/features/relay/contract/limits'
import { loadPairing, rotateTicket } from '@/features/relay/keyStore'
import { asRecord } from '@/shared/asRecord'
import { RELAY_URL } from '@/shared/config'

const HANDLE_TIMEOUT_MS = 15_000
const GONE = 410
const BAD_REQUEST = 400
const INVALID_TICKET = 'invalid_ticket'
const ISSUED_FIELDS = ['handle', 'ticket', 'ticketExpiresAt'] as const

export class TicketRejectedError extends Error {
  constructor() {
    super('the relay no longer accepts this browser ticket')
    this.name = 'TicketRejectedError'
  }
}

export class RelayUnreachableError extends Error {
  constructor(cause: unknown) {
    super('the relay did not issue a session handle', { cause })
    this.name = 'RelayUnreachableError'
  }
}

interface Issued {
  handle: string
  ticket: string
  ticketExpiresAt: string
}

function readIssued(body: unknown): Issued | undefined {
  const fields = asRecord(body)
  if (!fields) return undefined
  if (!ISSUED_FIELDS.every((field) => typeof fields[field] === 'string' && fields[field] !== '')) return undefined
  return { handle: String(fields.handle), ticket: String(fields.ticket), ticketExpiresAt: String(fields.ticketExpiresAt) }
}

async function isRejection(response: Response): Promise<boolean> {
  if (response.status === GONE) return true
  if (response.status !== BAD_REQUEST) return false
  const body: unknown = await response.json().catch(() => undefined)
  return asRecord(body)?.code === INVALID_TICKET
}

async function requestHandle(relayUrl: string, ticket: string): Promise<Response> {
  try {
    return await fetch(`${relayUrl}/session/web`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ v: CONTRACT_VERSION, ticket }),
      signal: AbortSignal.timeout(HANDLE_TIMEOUT_MS),
    })
  } catch (error: unknown) {
    throw new RelayUnreachableError(error)
  }
}

export async function fetchHandle(relayUrl = RELAY_URL): Promise<string> {
  const pairing = await loadPairing()
  if (!pairing) throw new TicketRejectedError()
  const response = await requestHandle(relayUrl, pairing.ticket)
  if (await isRejection(response)) throw new TicketRejectedError()
  const issued = response.ok ? readIssued(await response.json().catch(() => undefined)) : undefined
  if (!issued) throw new RelayUnreachableError(response.status)
  await rotateTicket(pairing.channel, issued.ticket, issued.ticketExpiresAt)
  return issued.handle
}
