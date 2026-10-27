import { afterEach, describe, expect, it, vi } from 'vitest'
import worker from './index.ts'
import { BrokenLimiter, FakeLimiter, RELAY, SITE_ORIGIN, fakeEnv, relayRequest } from './test/fakeEnv.ts'

const ADDRESS = '192.0.2.10'
const NEIGHBOUR = '192.0.2.20'
const FIRST_NEIGHBOUR = '192.0.2.1'
const SECOND_NEIGHBOUR = '192.0.2.2'
const THIRD_NEIGHBOUR = '192.0.2.3'
const OTHER_NETWORK = '198.51.100.10'
const SPOOFED = '203.0.113.66'
const IPV6_ADDRESS = '2001:db8:1:2::5'

function originReturns(response: Response = new Response('origin')) {
  return vi.spyOn(globalThis, 'fetch').mockResolvedValue(response)
}

function forwarded(origin: ReturnType<typeof originReturns>): Request {
  const sent = origin.mock.calls[0]?.[0]
  if (!(sent instanceof Request)) {
    throw new TypeError('origin was not called with a Request')
  }
  return sent
}

function upgradeResponse(): Response {
  const [client] = Object.values(new WebSocketPair())
  return new Response(null, { status: 101, webSocket: client ?? null })
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('edge worker', () => {
  it('should pass an allowed websocket upgrade to origin', async () => {
    const originResponse = upgradeResponse()
    const origin = originReturns(originResponse)
    const request = relayRequest('/hub?access_token=t', ADDRESS, { headers: { Upgrade: 'websocket' } })

    const result = await worker.fetch(request, fakeEnv())

    const sent = forwarded(origin)
    expect({ url: sent.url, method: sent.method, upgrade: sent.headers.get('Upgrade') }).toEqual({
      url: request.url,
      method: 'GET',
      upgrade: 'websocket',
    })
    expect(result).toBe(originResponse)
  })

  it('should send the verified address to origin as X-Real-IP', async () => {
    const origin = originReturns()
    const request = relayRequest('/pair/nonce', ADDRESS, {
      method: 'POST',
      body: '{"v":1}',
      headers: { 'X-Real-IP': SPOOFED },
    })

    await worker.fetch(request, fakeEnv())

    const sent = forwarded(origin)
    expect({
      realIp: sent.headers.get('X-Real-IP'),
      method: sent.method,
      body: await sent.text(),
    }).toEqual({ realIp: ADDRESS, method: 'POST', body: '{"v":1}' })
  })

  it('should drop a client X-Forwarded-For before origin', async () => {
    const origin = originReturns()
    const request = relayRequest('/pair/nonce', ADDRESS, { method: 'POST', headers: { 'X-Forwarded-For': SPOOFED } })

    await worker.fetch(request, fakeEnv())

    expect(forwarded(origin).headers.get('X-Forwarded-For')).toBeNull()
  })

  it('should drop a client X-Real-IP when no verified address arrives', async () => {
    const origin = originReturns()
    const request = new Request(`${RELAY}/pair/nonce`, { method: 'POST', headers: { 'X-Real-IP': SPOOFED } })

    await worker.fetch(request, fakeEnv())

    expect(forwarded(origin).headers.get('X-Real-IP')).toBeNull()
  })

  it('should refuse with 429 when the address spends its budget', async () => {
    const origin = originReturns()
    const env = fakeEnv({ PAIR_CLIENT: new FakeLimiter(1) })
    await worker.fetch(relayRequest('/pair/nonce', ADDRESS, { method: 'POST' }), env)

    const result = await worker.fetch(relayRequest('/pair/start', ADDRESS, { method: 'POST' }), env)

    expect({
      status: result.status,
      retryAfter: result.headers.get('Retry-After'),
      type: result.headers.get('Content-Type'),
      cacheControl: result.headers.get('Cache-Control'),
      vary: result.headers.get('Vary'),
      body: await result.json(),
      originCalls: origin.mock.calls.length,
    }).toEqual({
      status: 429,
      retryAfter: '60',
      type: 'application/problem+json',
      cacheControl: 'no-store',
      vary: 'Origin',
      body: { type: 'https://tools.ietf.org/html/rfc6585#section-4', title: 'rate_limited', status: 429, code: 'rate_limited' },
      originCalls: 1,
    })
  })

  it('should refuse when the network has spent its budget', async () => {
    const origin = originReturns()
    const env = fakeEnv({ SESSION_NETWORK: new FakeLimiter(0) })

    const result = await worker.fetch(relayRequest('/session/cli', ADDRESS, { method: 'POST' }), env)

    expect({ status: result.status, originCalls: origin.mock.calls.length }).toEqual({ status: 429, originCalls: 0 })
  })

  it('should still refuse on the network tier when the address limiter fails', async () => {
    const origin = originReturns()
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const env = fakeEnv({ PAIR_CLIENT: new BrokenLimiter(), PAIR_NETWORK: new FakeLimiter(0) })

    const result = await worker.fetch(relayRequest('/pair/claim', ADDRESS, { method: 'POST' }), env)

    expect({ status: result.status, originCalls: origin.mock.calls.length }).toEqual({ status: 429, originCalls: 0 })
  })

  it('should share one budget across addresses in a network', async () => {
    originReturns()
    const env = fakeEnv({ HUB_CLIENT: new FakeLimiter(1), HUB_NETWORK: new FakeLimiter(2) })
    await worker.fetch(relayRequest('/hub', FIRST_NEIGHBOUR), env)
    await worker.fetch(relayRequest('/hub', SECOND_NEIGHBOUR), env)

    const sameNetwork = await worker.fetch(relayRequest('/hub', THIRD_NEIGHBOUR), env)
    const otherNetwork = await worker.fetch(relayRequest('/hub', OTHER_NETWORK), env)

    expect([sameNetwork.status, otherNetwork.status]).toEqual([429, 200])
  })

  it('should not charge the network for requests over the address limit', async () => {
    originReturns()
    const env = fakeEnv({ PAIR_CLIENT: new FakeLimiter(1), PAIR_NETWORK: new FakeLimiter(2) })
    for (let attempt = 0; attempt < 5; attempt += 1) {
      await worker.fetch(relayRequest('/pair/claim', ADDRESS, { method: 'POST' }), env)
    }

    const result = await worker.fetch(relayRequest('/pair/claim', NEIGHBOUR, { method: 'POST' }), env)

    expect(result.status).toBe(200)
  })

  it('should keep a separate budget per path group', async () => {
    originReturns()
    const env = fakeEnv({ PAIR_CLIENT: new FakeLimiter(0) })

    const result = await worker.fetch(relayRequest('/session/web', ADDRESS, { method: 'POST' }), env)

    expect(result.status).toBe(200)
  })

  it('should count CORS preflights apart from requests', async () => {
    originReturns()
    const env = fakeEnv({ PAIR_CLIENT: new FakeLimiter(1) })
    await worker.fetch(relayRequest('/pair/claim', ADDRESS, { method: 'OPTIONS' }), env)

    const result = await worker.fetch(relayRequest('/pair/claim', ADDRESS, { method: 'POST' }), env)

    expect(result.status).toBe(200)
  })

  it('should answer 404 for paths outside the relay groups', async () => {
    const origin = originReturns()

    const result = await worker.fetch(relayRequest('/pair/%2e%2e/healthz', ADDRESS), fakeEnv())

    expect({ status: result.status, originCalls: origin.mock.calls.length }).toEqual({ status: 404, originCalls: 0 })
  })

  it('should pass the request through when the limiter fails', async () => {
    const originResponse = new Response('origin')
    originReturns(originResponse)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const env = fakeEnv({ PAIR_CLIENT: new BrokenLimiter(), PAIR_NETWORK: new BrokenLimiter() })

    const result = await worker.fetch(relayRequest('/pair/nonce', ADDRESS, { method: 'POST' }), env)

    expect(result).toBe(originResponse)
  })

  it('should log one line per failed limiter without address or error text', async () => {
    originReturns()
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const env = fakeEnv({ PAIR_CLIENT: new BrokenLimiter(), PAIR_NETWORK: new BrokenLimiter() })

    await worker.fetch(relayRequest('/pair/nonce', ADDRESS, { method: 'POST' }), env)

    const lines = error.mock.calls.map(([line]) => JSON.parse(String(line)))
    const message = 'edge rate limit check failed, request passed'
    expect(lines).toEqual([
      { message, group: 'pair', tier: 'client', reason: 'Error' },
      { message, group: 'pair', tier: 'network', reason: 'Error' },
    ])
  })

  it.each([
    ['site', SITE_ORIGIN, SITE_ORIGIN, 'Retry-After'],
    ['other', 'https://other.example.test', null, null],
  ])('should give a 429 for the %s origin its CORS headers', async (_name, requestOrigin, allowOrigin, exposed) => {
    originReturns()
    const env = fakeEnv({ PAIR_CLIENT: new FakeLimiter(0) })

    const result = await worker.fetch(
      relayRequest('/pair/claim', ADDRESS, { method: 'POST', headers: { Origin: requestOrigin } }),
      env,
    )

    expect({
      allowOrigin: result.headers.get('Access-Control-Allow-Origin'),
      exposed: result.headers.get('Access-Control-Expose-Headers'),
    }).toEqual({ allowOrigin, exposed })
  })

  it('should log refusals without the client address', async () => {
    originReturns()
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const env = fakeEnv({ HUB_CLIENT: new FakeLimiter(0), PAIR_NETWORK: new FakeLimiter(0) })
    await worker.fetch(relayRequest('/hub', IPV6_ADDRESS), env)

    await worker.fetch(relayRequest('/pair/nonce', ADDRESS, { method: 'POST' }), env)

    const lines = warn.mock.calls.map(([line]) => JSON.parse(String(line)))
    const message = 'edge rate limit refused request'
    expect(lines).toEqual([
      { message, group: 'hub', tier: 'client' },
      { message, group: 'pair', tier: 'network' },
    ])
  })
})
