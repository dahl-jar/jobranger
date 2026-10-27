import { afterEach, describe, expect, it, vi } from 'vitest'
import { claimPairing } from '@/features/pairing/claimPairing'

const RELAY = 'https://relay.example.test'
const CHANNEL = 'QzATLaCZ1ciBwANAgYReA3Np4WJ48r7ZPzlZsVwdxSQ'
const CLAIMED = {
  channel: CHANNEL,
  handle: 'handle-one',
  handleExpiresAt: '2026-10-13T08:05:00Z',
  ticket: 'ticket-one',
  ticketExpiresAt: '2027-01-11T08:00:00Z',
}

function relayAnswers(status: number, body: unknown = {}) {
  const fetchMock = vi.fn(async () => new Response(JSON.stringify(body), { status }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('claimPairing', () => {
  it('should post the formatted code to /pair/claim', async () => {
    const fetchMock = relayAnswers(200, CLAIMED)

    await claimPairing('KQFBMXTW', RELAY)

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect([url, init.method, new Headers(init.headers).get('content-type'), JSON.parse(String(init.body))]).toEqual([
      `${RELAY}/pair/claim`,
      'POST',
      'application/json',
      { v: 1, code: 'KQFB-MXTW' },
    ])
  })

  it('should return the channel, handle and ticket on 200', async () => {
    relayAnswers(200, CLAIMED)

    expect(await claimPairing('KQFBMXTW', RELAY)).toEqual({
      kind: 'ok',
      channel: CLAIMED.channel,
      handle: CLAIMED.handle,
      ticket: CLAIMED.ticket,
      ticketExpiresAt: CLAIMED.ticketExpiresAt,
    })
  })

  it.each([
    { status: 400, kind: 'invalid' },
    { status: 404, kind: 'wrong' },
    { status: 410, kind: 'expired' },
    { status: 429, kind: 'rateLimited' },
    { status: 503, kind: 'unreachable' },
  ])('should map $status to $kind', async ({ status, kind }) => {
    relayAnswers(status)

    expect(await claimPairing('KQFBMXTW', RELAY)).toEqual({ kind })
  })

  it.each([
    { name: 'a missing ticket', body: { ...CLAIMED, ticket: undefined } },
    { name: 'a missing handle expiry', body: { ...CLAIMED, handleExpiresAt: undefined } },
    { name: 'a channel that is not 32 bytes', body: { ...CLAIMED, channel: 'AAAA' } },
    { name: 'a channel that is not base64url', body: { ...CLAIMED, channel: CHANNEL.replace('Q', '+') } },
    { name: 'an empty handle', body: { ...CLAIMED, handle: '' } },
    { name: 'a null body', body: null },
    { name: 'a string body', body: 'ok' },
  ])('should call a 200 with $name unreachable', async ({ body }) => {
    relayAnswers(200, body)

    expect(await claimPairing('KQFBMXTW', RELAY)).toEqual({ kind: 'unreachable' })
  })

  it('should call a network failure unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('Failed to fetch'))))

    expect(await claimPairing('KQFBMXTW', RELAY)).toEqual({ kind: 'unreachable' })
  })
})
