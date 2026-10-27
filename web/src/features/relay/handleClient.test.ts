import 'fake-indexeddb/auto'
import { createStore, get } from 'idb-keyval'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fetchHandle, RelayUnreachableError, TicketRejectedError } from '@/features/relay/handleClient'
import { clearPairing, loadPairing, savePairing, type StoredPairing } from '@/features/relay/keyStore'
import { TEST_CHANNEL } from '@/test/fakeRelay'
import { storedPairing } from '@/test/relayFixtures'

const race = vi.hoisted(() => ({ armed: false }))

vi.mock('idb-keyval', async (importOriginal) => {
  const actual = await importOriginal<typeof import('idb-keyval')>()
  return {
    ...actual,
    get: async (...args: Parameters<typeof actual.get>) => {
      const value = await actual.get(...args)
      if (race.armed) {
        race.armed = false
        await actual.del(args[0], args[1])
      }
      return value
    },
    update: async (...args: Parameters<typeof actual.update>) => {
      if (race.armed) {
        race.armed = false
        await actual.del(args[0], args[2])
      }
      return actual.update(...args)
    },
  }
})

const RELAY = 'https://relay.example.test'
const HANDLE_TIMEOUT_MS = 15_000
const ISSUED = {
  channel: TEST_CHANNEL,
  handle: 'handle-two',
  handleExpiresAt: '2026-10-13T08:05:00Z',
  ticket: 'ticket-two',
  ticketExpiresAt: '2027-01-11T08:00:00Z',
}

function relayAnswers(status: number, body: unknown = {}, before: () => Promise<void> = async () => undefined) {
  const fetchMock = vi.fn(async () => {
    await before()
    return new Response(typeof body === 'string' ? body : JSON.stringify(body), { status })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

let stored: StoredPairing

beforeEach(async () => {
  stored = await storedPairing({ ticketExpiresAt: '2027-01-01T00:00:00Z' })
  await savePairing(stored)
})

afterEach(async () => {
  race.armed = false
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  await clearPairing()
})

describe('fetchHandle', () => {
  it('should post the stored ticket to /session/web', async () => {
    const fetchMock = relayAnswers(200, ISSUED)

    await fetchHandle(RELAY)

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect([url, init.method, new Headers(init.headers).get('content-type'), JSON.parse(String(init.body))]).toEqual([
      `${RELAY}/session/web`,
      'POST',
      'application/json',
      { v: 1, ticket: 'ticket-one' },
    ])
  })

  it('should return the new handle', async () => {
    relayAnswers(200, ISSUED)

    expect(await fetchHandle(RELAY)).toBe('handle-two')
  })

  it('should save the new ticket and its expiry', async () => {
    relayAnswers(200, ISSUED)

    await fetchHandle(RELAY)

    const stored = await loadPairing()
    expect([stored?.ticket, stored?.ticketExpiresAt, stored?.cliStaticPublicKey?.length]).toEqual(['ticket-two', '2027-01-11T08:00:00Z', 32])
  })

  it('should not bring back a pairing cleared while the request ran', async () => {
    relayAnswers(200, ISSUED, clearPairing)

    await fetchHandle(RELAY)

    expect(await loadPairing()).toBeUndefined()
  })

  it('should not bring back a pairing cleared while the new ticket is stored', async () => {
    relayAnswers(200, ISSUED, async () => {
      race.armed = true
    })

    await fetchHandle(RELAY)

    expect(await get('current', createStore('jobranger', 'pairing'))).toBeUndefined()
  })

  it('should leave a pairing made while the request ran untouched', async () => {
    const replacement = { ...stored, channel: 'channel-two', ticket: 'ticket-three' }
    relayAnswers(200, ISSUED, () => savePairing(replacement))

    await fetchHandle(RELAY)

    expect((await loadPairing())?.ticket).toBe('ticket-three')
  })

  it.each([
    [410, 'ticket_expired'],
    [400, 'invalid_ticket'],
  ])('should reject the ticket on %i %s', async (status, code) => {
    relayAnswers(status, { code })

    await expect(fetchHandle(RELAY)).rejects.toBeInstanceOf(TicketRejectedError)
  })

  it.each([
    ['another 400 code', 400, { code: 'invalid_request' }],
    ['a server error', 503, {}],
    ['a 200 without a handle', 200, { ...ISSUED, handle: '' }],
    ['a 200 with a handle that is not text', 200, { ...ISSUED, handle: 7 }],
    ['a 200 that is not JSON', 200, 'not json'],
    ['a 400 that is not JSON', 400, 'not json'],
  ])('should report the relay unreachable on %s', async (_, status, body) => {
    relayAnswers(status, body)

    await expect(fetchHandle(RELAY)).rejects.toBeInstanceOf(RelayUnreachableError)
  })

  it('should give the relay 15 seconds to answer', async () => {
    const timeout = vi.spyOn(AbortSignal, 'timeout')
    relayAnswers(200, ISSUED)

    await fetchHandle(RELAY)

    expect(timeout).toHaveBeenCalledWith(HANDLE_TIMEOUT_MS)
  })

  it('should give up on a relay that never answers', async () => {
    vi.spyOn(AbortSignal, 'timeout').mockReturnValue(AbortSignal.abort(new DOMException('relay too slow', 'TimeoutError')))
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_: string, init: RequestInit) =>
          new Promise((_resolve, reject) => {
            if (init.signal?.aborted) reject(init.signal.reason)
            init.signal?.addEventListener('abort', () => reject(init.signal?.reason))
          }),
      ),
    )

    await expect(fetchHandle(RELAY)).rejects.toBeInstanceOf(RelayUnreachableError)
  })

  it('should report the relay unreachable when the request fails', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('network down'))))

    await expect(fetchHandle(RELAY)).rejects.toBeInstanceOf(RelayUnreachableError)
  })

  it('should reject the ticket when nothing is stored', async () => {
    const fetchMock = relayAnswers(200, ISSUED)
    await clearPairing()

    await expect(fetchHandle(RELAY)).rejects.toBeInstanceOf(TicketRejectedError)
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
