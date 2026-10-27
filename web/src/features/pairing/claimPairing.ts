import { formatPairCode } from '@/features/pairing/pairCode'
import { decodeBase64Url } from '@/features/relay/base64url'
import { CONTRACT_VERSION } from '@/features/relay/contract/limits'
import { asRecord } from '@/shared/asRecord'
import { RELAY_URL } from '@/shared/config'

const CLAIM_TIMEOUT_MS = 15_000
const CHANNEL_BYTES = 32
const CLAIM_FIELDS = ['channel', 'handle', 'handleExpiresAt', 'ticket', 'ticketExpiresAt'] as const

export interface ClaimedPairing {
  channel: string
  handle: string
  ticket: string
  ticketExpiresAt: string
}

export type ClaimResult =
  | ({ kind: 'ok' } & ClaimedPairing)
  | { kind: 'invalid' }
  | { kind: 'wrong' }
  | { kind: 'expired' }
  | { kind: 'rateLimited' }
  | { kind: 'unreachable' }

const STATUS_RESULTS: Record<number, ClaimResult> = {
  400: { kind: 'invalid' },
  404: { kind: 'wrong' },
  410: { kind: 'expired' },
  429: { kind: 'rateLimited' },
}

const UNREACHABLE: ClaimResult = { kind: 'unreachable' }

function isChannel(value: string): boolean {
  try {
    return decodeBase64Url(value).length === CHANNEL_BYTES
  } catch {
    return false
  }
}

function isClaimedPairing(body: unknown): body is ClaimedPairing {
  const fields = asRecord(body)
  if (fields === undefined) return false
  return CLAIM_FIELDS.every((field) => typeof fields[field] === 'string' && fields[field] !== '') && isChannel(String(fields.channel))
}

function readClaim(body: unknown): ClaimResult {
  if (!isClaimedPairing(body)) return UNREACHABLE
  const { channel, handle, ticket, ticketExpiresAt } = body
  return { kind: 'ok', channel, handle, ticket, ticketExpiresAt }
}

export async function claimPairing(code: string, relayUrl = RELAY_URL): Promise<ClaimResult> {
  try {
    const response = await fetch(`${relayUrl}/pair/claim`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ v: CONTRACT_VERSION, code: formatPairCode(code) }),
      signal: AbortSignal.timeout(CLAIM_TIMEOUT_MS),
    })
    if (response.ok) return readClaim(await response.json())
    return STATUS_RESULTS[response.status] ?? UNREACHABLE
  } catch {
    return UNREACHABLE
  }
}
