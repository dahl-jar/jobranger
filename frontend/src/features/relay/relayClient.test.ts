import { afterEach, describe, expect, it, vi } from 'vitest'
import { splitMessage } from '@/features/relay/chunks'
import type { ConnectionState } from '@/features/relay/connectionStore'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { CvUpload, Payload } from '@/features/relay/contract/payload.gen'
import { RelayUnreachableError, TicketRejectedError } from '@/features/relay/handleClient'
import { connectHub } from '@/features/relay/hubTransport'
import type { StoredPairing } from '@/features/relay/keyStore'
import { readLastSeen } from '@/features/relay/lastSeen'
import { RelayClient, type RelayClientDeps } from '@/features/relay/relayClient'
import { RESTART_BACKOFF_MS } from '@/features/relay/sessionRunner'
import { FakeHub } from '@/test/fakeHub'
import { CliPeer, FakeRelay, type PeerOptions } from '@/test/fakeRelay'
import { flush, until } from '@/test/flush'
import {
  answerUnknownDevice,
  failingFirstHandle,
  LAST_SEEN_KEY,
  LONG_DELAY_MS,
  memoryStorage,
  SEEN_AT,
  storedPairing,
  TEST_AGENT,
  withoutCliKey,
} from '@/test/relayFixtures'

const STATUS = JSON.stringify(payloadFixtures.status)
const EARLIER = '2026-10-12T09:00:00.000Z'
const SHORT_DELAY_MS = 50
const HANDLE_TIMEOUT_MS = 15_000
const SLOW_ATTEMPT_MS = 5_000
const HUB_TIMEOUT_MS = 30_000

interface Options {
  pairing?: Partial<StoredPairing>
  adjust?: (pairing: StoredPairing) => StoredPairing
  peer?: PeerOptions
  fetchHandle?: () => Promise<string>
  retryDelay?: (previousRetryCount: number) => number
  seen?: Record<string, string>
  stored?: boolean
  loadGate?: Promise<void>
  extra?: Partial<RelayClientDeps>
}

let client: RelayClient | undefined

afterEach(async () => {
  await client?.stop()
  client = undefined
  vi.useRealTimers()
})

async function setup(options: Options = {}) {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay, options.peer)
  const states: ConnectionState[] = []
  const messages: Payload[] = []
  const seenStorage = memoryStorage(options.seen)
  const counts = { cleared: 0, disconnects: 0, handles: 0, resets: 0, readies: 0 }
  const pairing = await storedPairing({ cliStaticPublicKey: peer.cli.staticPublic, ...options.pairing })
  client = new RelayClient({
    loadPairing: async () => {
      await options.loadGate
      return options.stored === false ? undefined : (options.adjust?.(pairing) ?? pairing)
    },
    clearPairingOf: async () => void (counts.cleared += 1),
    fetchHandle:
      options.fetchHandle ??
      (async () => {
        counts.handles += 1
        return `handle-${counts.handles}`
      }),
    connect: relay.connect,
    agent: TEST_AGENT,
    retryDelay: options.retryDelay ?? (() => LONG_DELAY_MS),
    seenStorage,
    onState: (state) => states.push(state),
    onMessage: (message) => messages.push(message),
    onDisconnect: () => void (counts.disconnects += 1),
    onSessionReset: () => void (counts.resets += 1),
    onReady: () => void (counts.readies += 1),
    ...options.extra,
  })
  return { client, relay, peer, states, messages, seen: seenStorage.entries, seenStorage, counts, last: () => states.at(-1) }
}

type Context = Awaited<ReturnType<typeof setup>>

async function failAfter(step: 'handle' | 'hub', elapsedMs: number): Promise<Context> {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date(SEEN_AT))
  const fail = async (): Promise<never> => {
    vi.setSystemTime(Date.now() + elapsedMs)
    throw new RelayUnreachableError('timed out')
  }
  const retryDelay = (count: number) => (count === 0 ? 0 : SHORT_DELAY_MS)
  return setup(step === 'handle' ? { retryDelay, fetchHandle: fail } : { retryDelay, extra: { connect: fail } })
}

async function ready(options: Options = {}): Promise<Context> {
  const context = await setup(options)
  await context.client.start()
  context.relay.presence(true)
  await until(() => context.last()?.kind === 'ready')
  return context
}

describe('RelayClient', () => {
  it('should authenticate the hub with the handle alone', async () => {
    const hub = await FakeHub.start()
    const context = await setup({ extra: { connect: connectHub(hub.url) } })

    await context.client.start()
    hub.call('Presence', { cli_online: true, browsers: 1 })
    await until(() => hub.invocations.length > 0)

    expect([hub.tokens, hub.invocations.map((invocation) => invocation.target)]).toEqual([['handle-1'], ['Send']])
    await context.client.stop()
    await hub.stop()
  })

  it('should report unpaired without connecting when nothing is stored', async () => {
    const context = await setup({ stored: false })

    await context.client.start()

    expect([context.states, context.relay.handles, context.counts.cleared]).toEqual([[{ kind: 'unpaired' }], [], 0])
  })

  it.each<[string, (pairing: StoredPairing) => StoredPairing]>([
    ['pending', (pairing) => ({ ...pairing, pending: true })],
    ['missing its CLI key', withoutCliKey],
  ])('should clear a pairing that is %s and run no handshake', async (_, adjust) => {
    const context = await setup({ adjust })

    await context.client.start()

    expect([context.states, context.counts.cleared, context.relay.handles]).toEqual([[{ kind: 'unpaired' }], 1, []])
  })

  it('should forget the pairing when the ticket is rejected', async () => {
    const context = await setup({ fetchHandle: async () => Promise.reject(new TicketRejectedError()) })

    await context.client.start()

    expect([context.last(), context.counts.cleared]).toEqual([{ kind: 'unpaired', reason: 'ticket' }, 1])
  })

  it('should report the relay down until the retry time', async () => {
    const context = await setup({ retryDelay: () => SHORT_DELAY_MS, fetchHandle: failingFirstHandle() })
    const before = Date.now()

    await context.client.start()

    expect(context.last()).toEqual({ kind: 'relayDown', retryAt: expect.closeTo(before + SHORT_DELAY_MS, -2) })
  })

  it('should connect when the retry time comes', async () => {
    const context = await setup({ retryDelay: () => SHORT_DELAY_MS, fetchHandle: failingFirstHandle() })

    await context.client.start()
    await until(() => context.relay.handles.length > 0)

    expect(context.relay.handles).toEqual(['handle-two'])
  })

  it('should connect again at once when the retry delay is zero', async () => {
    const context = await setup({
      retryDelay: () => 0,
      fetchHandle: failingFirstHandle(),
    })

    await context.client.start()
    await until(() => context.relay.handles.length > 0)

    expect(context.states.map((state) => state.kind)).toEqual(['connecting', 'connecting'])
  })

  it('should retry at once when the user retries', async () => {
    const context = await setup({
      fetchHandle: failingFirstHandle(),
    })
    await context.client.start()

    context.client.retryNow()
    await until(() => context.relay.handles.length > 0)

    expect(context.relay.handles).toEqual(['handle-two'])
  })

  it.each([
    ['the handle request', 'handle', HANDLE_TIMEOUT_MS],
    ['the hub handshake', 'hub', HUB_TIMEOUT_MS],
    ['an attempt that takes exactly 5 seconds', 'handle', SLOW_ATTEMPT_MS],
  ] as const)('should show the relay down at once when %s times out', async (_, step, elapsedMs) => {
    const context = await failAfter(step, elapsedMs)

    await context.client.start()

    expect(context.states).toEqual([{ kind: 'connecting' }, { kind: 'relayDown', retryAt: Date.parse(SEEN_AT) + elapsedMs + SHORT_DELAY_MS }])
  })

  it('should keep the next scheduled delay after a slow attempt that follows a fast one', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(SEEN_AT))
    let attempts = 0
    const context = await setup({
      retryDelay: (count) => [0, SHORT_DELAY_MS][count] ?? LONG_DELAY_MS,
      fetchHandle: async () => {
        attempts += 1
        if (attempts === 2) vi.setSystemTime(Date.now() + HANDLE_TIMEOUT_MS)
        throw new RelayUnreachableError('offline')
      },
    })

    await context.client.start()
    await until(() => context.last()?.kind === 'relayDown')

    expect(context.last()).toEqual({ kind: 'relayDown', retryAt: Date.parse(SEEN_AT) + HANDLE_TIMEOUT_MS + SHORT_DELAY_MS })
  })

  it('should connect again at once when an attempt fails just under 5 seconds', async () => {
    const context = await failAfter('handle', SLOW_ATTEMPT_MS - 1)

    await context.client.start()
    await until(() => context.states.length > 1)

    expect(context.states.slice(0, 2)).toEqual([{ kind: 'connecting' }, { kind: 'connecting' }])
  })

  it('should connect again at once when the hub refuses the connection fast', async () => {
    let attempts = 0
    const context = await setup({
      retryDelay: (count) => (count === 0 ? 0 : LONG_DELAY_MS),
      extra: {
        connect: async () => {
          attempts += 1
          throw new Error('socket refused')
        },
      },
    })

    await context.client.start()
    await until(() => attempts === 2)

    expect(context.states.map((state) => state.kind)).toEqual(['connecting', 'connecting', 'relayDown'])
  })

  it('should reconnect at once when a long session drops', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(SEEN_AT))
    const context = await ready({ retryDelay: (count) => (count === 0 ? 0 : LONG_DELAY_MS) })
    context.relay.onlineAtConnect = true
    vi.setSystemTime(Date.now() + HUB_TIMEOUT_MS)

    context.relay.drop()
    await until(() => context.relay.handles.length === 2)

    expect(context.states.some((state) => state.kind === 'relayDown')).toBe(false)
  })

  it('should become ready after a handshake with the stored CLI key', async () => {
    const context = await ready()

    expect(context.states.map((state) => state.kind)).toEqual(['connecting', 'handshaking', 'ready'])
  })

  it('should pass CLI messages on', async () => {
    const context = await ready()

    await context.peer.sendData(STATUS)
    await until(() => context.messages.length > 0)

    expect(context.messages).toEqual([payloadFixtures.status])
  })

  it('should record when the machine was last heard from', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(SEEN_AT))
    const context = await ready()

    await context.peer.sendData(STATUS)
    await until(() => context.messages.length > 0)

    expect(context.seen[LAST_SEEN_KEY]).toBe(SEEN_AT)
  })

  it('should parse chunked messages with the given parser', async () => {
    const texts: string[] = []
    const context = await ready({ extra: { parseAssembled: async (text) => (texts.push(text), undefined) } })
    const parts = splitMessage({ ...(payloadFixtures['cv.upload'] as CvUpload), content_b64: 'A'.repeat(100 * 1024) })

    for (const part of parts) await context.peer.sendData(JSON.stringify(part))
    await until(() => texts.length > 0)

    expect(texts).toHaveLength(1)
  })

  it('should report the machine offline with the time it was last seen when presence drops', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(SEEN_AT))
    const context = await ready({ seen: { [LAST_SEEN_KEY]: EARLIER } })

    context.relay.presence(false)

    expect([context.last(), context.counts.disconnects]).toEqual([{ kind: 'machineOffline', lastSeen: SEEN_AT }, 1])
  })

  it('should report the machine offline when it is offline at connect', async () => {
    const context = await setup({ seen: { [LAST_SEEN_KEY]: EARLIER } })
    await context.client.start()

    context.relay.presence(false)
    await flush()

    expect([context.last(), context.relay.sent]).toEqual([{ kind: 'machineOffline', lastSeen: EARLIER }, []])
  })

  it('should start over with a new handle when the connection drops', async () => {
    const context = await ready({ retryDelay: () => 0 })
    context.relay.onlineAtConnect = true

    context.relay.drop()
    await until(() => context.relay.handles.length === 2 && context.relay.sids().length === 2)

    expect([context.relay.handles, context.counts.disconnects]).toEqual([['handle-1', 'handle-2'], 1])
  })

  it('should pause edits while the session restarts after an abort', async () => {
    const context = await ready()

    context.peer.sendHello({ t: 'abort' })
    await until(() => context.last()?.kind !== 'ready')

    expect([context.last(), context.counts.disconnects]).toEqual([{ kind: 'handshaking' }, 1])
  })

  it('should keep reconnecting through more healthy session closes than the restart cap', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await ready()

    for (let close = 0; close <= RESTART_BACKOFF_MS.length; close += 1) {
      const handshakes = context.states.filter((state) => state.kind === 'ready').length
      context.peer.sendHello({ t: 'abort' })
      await until(() => context.last()?.kind === 'handshaking')
      vi.advanceTimersByTime(RESTART_BACKOFF_MS[0] ?? 0)
      await until(() => context.states.filter((state) => state.kind === 'ready').length > handshakes)
    }

    expect(context.states.some((state) => state.kind === 'failed')).toBe(false)
  })

  it('should hold pending commands when the machine no longer knows the session', async () => {
    const context = await ready()

    context.peer.sendHello({ t: 'unknown_session' })
    await until(() => context.last()?.kind === 'handshaking')

    expect([context.counts.resets, context.counts.disconnects]).toEqual([1, 0])
  })

  it('should report ready for each new session', async () => {
    const context = await ready()

    context.peer.sendHello({ t: 'unknown_session' })
    await until(() => context.counts.readies === 2)

    expect(context.counts.readies).toBe(2)
  })

  it('should close the session when the network goes offline', async () => {
    const context = await ready()

    context.client.pauseForNetwork()
    await until(() => context.relay.stopped)

    expect(context.last()).toEqual({ kind: 'networkOffline' })
  })

  it('should not retry the relay while the network is offline', async () => {
    const context = await setup({ retryDelay: () => SHORT_DELAY_MS, fetchHandle: async () => Promise.reject(new RelayUnreachableError('offline')) })
    await context.client.start()

    context.client.pauseForNetwork()
    await flush()

    expect([context.last(), context.counts.handles]).toEqual([{ kind: 'networkOffline' }, 0])
  })

  it('should not retry when the network drops while a handle request runs', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => (release = resolve))
    const attempts: number[] = []
    const context = await setup({
      retryDelay: () => 0,
      fetchHandle: async () => {
        attempts.push(1)
        await gate
        throw new RelayUnreachableError('offline')
      },
    })
    const starting = context.client.start()
    await until(() => attempts.length > 0)

    context.client.pauseForNetwork()
    release()
    await starting
    await flush()

    expect([attempts.length, context.last()]).toEqual([1, { kind: 'networkOffline' }])
  })

  it('should close a connection that opens after the network dropped', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => (release = resolve))
    const relay = new FakeRelay()
    const context = await setup({
      extra: {
        connect: async (handle, events) => {
          await gate
          return relay.connect(handle, events)
        },
      },
    })
    const starting = context.client.start()
    await until(() => context.counts.handles > 0)

    context.client.pauseForNetwork()
    release()
    await starting
    await flush()

    expect([relay.handles, relay.stopped]).toEqual([['handle-1'], true])
  })

  it('should fail pending commands once when the browser says offline twice', async () => {
    const context = await ready()

    context.client.pauseForNetwork()
    context.client.pauseForNetwork()

    expect(context.counts.disconnects).toBe(1)
  })

  it('should connect at once when the network comes back', async () => {
    const context = await ready()
    context.client.pauseForNetwork()

    context.client.resumeForNetwork()
    await until(() => context.relay.handles.length === 2)

    expect(context.relay.handles).toEqual(['handle-1', 'handle-2'])
  })

  it('should wait for the network before the first connection', async () => {
    const context = await setup()
    context.client.pauseForNetwork()

    await context.client.start()

    expect([context.states, context.counts.handles]).toEqual([[{ kind: 'networkOffline' }, { kind: 'networkOffline' }], 0])
  })

  it('should not open a second connection when the network reports online while connected', async () => {
    const context = await ready()

    context.client.resumeForNetwork()
    await flush()

    expect(context.relay.handles).toEqual(['handle-1'])
  })

  it('should stop with a cli_key failure when the machine offers a different CLI key', async () => {
    const context = await setup({ pairing: { cliStaticPublicKey: new Uint8Array(32).fill(9) } })
    await context.client.start()

    context.relay.presence(true)
    await until(() => context.relay.stopped)

    expect([context.last(), context.counts.cleared, context.counts.disconnects]).toEqual([{ kind: 'failed', reason: 'cli_key' }, 0, 1])
  })

  it('should keep the pairing when the machine does not know this browser', async () => {
    const context = await setup({ peer: { replyToCommit: false } })
    await context.client.start()

    await answerUnknownDevice(context.relay, context.peer)
    await until(() => context.last()?.kind === 'failed')

    expect([context.last(), context.counts.cleared]).toEqual([{ kind: 'failed', reason: 'unknown_device' }, 0])
  })

  it('should unpair on the machine and in the browser', async () => {
    const context = await ready()

    await context.client.unpair()

    expect([context.peer.received.map((text) => (JSON.parse(text) as Payload).type), context.counts.cleared, context.last()]).toEqual([
      ['unpair'],
      1,
      { kind: 'unpaired' },
    ])
  })

  it('should forget the pairing only after a running handle request settles', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => (release = resolve))
    const order: string[] = []
    const context = await setup({
      fetchHandle: async () => {
        await gate
        order.push('handle')
        return 'handle-late'
      },
      extra: { clearPairingOf: async () => void order.push('cleared') },
    })
    const starting = context.client.start()
    await until(() => context.states.length > 0)

    const unpairing = context.client.unpair()
    await flush()
    release()
    await Promise.all([starting, unpairing])

    expect(order).toEqual(['handle', 'cleared'])
  })

  it('should clear the last seen time on unpair', async () => {
    const context = await ready({ seen: { [LAST_SEEN_KEY]: EARLIER } })

    await context.client.unpair()

    expect(readLastSeen(context.seenStorage)).toBeUndefined()
  })

  it('should clear the last seen time for an incomplete pairing', async () => {
    const context = await setup({ seen: { [LAST_SEEN_KEY]: EARLIER }, adjust: withoutCliKey })

    await context.client.start()

    expect(readLastSeen(context.seenStorage)).toBeUndefined()
  })

  it('should report unpaired when unpaired before any connection was tried', async () => {
    const context = await setup({ stored: false })

    await context.client.unpair()

    expect(context.last()).toEqual({ kind: 'unpaired', reason: undefined })
  })

  it('should not send unpair back when the machine unpaired this browser', async () => {
    const context = await ready()

    await context.client.unpair('revoked')

    expect([context.peer.received, context.counts.cleared, context.last()]).toEqual([[], 1, { kind: 'unpaired', reason: 'revoked' }])
  })

  it('should not reconnect after stop', async () => {
    const context = await ready({ retryDelay: () => 0 })

    await context.client.stop()
    context.relay.drop()
    await flush()

    expect(context.relay.handles).toEqual(['handle-1'])
  })

  it.each([
    ['a stored pairing', true],
    ['an empty store', false],
  ])('should stay quiet when stopped while loading %s', async (_, stored) => {
    let release: () => void = () => undefined
    const context = await setup({ stored, loadGate: new Promise((resolve) => (release = resolve)) })
    const starting = context.client.start()

    await context.client.stop()
    release()
    await starting

    expect([context.states, context.counts.handles]).toEqual([[], 0])
  })

  it.each([
    ['issues a handle', async () => 'handle-late'],
    ['fails', async () => Promise.reject(new RelayUnreachableError('offline'))],
  ])('should stay quiet when stopped while the relay %s', async (_, answer) => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => (release = resolve))
    const context = await setup({ fetchHandle: () => gate.then(answer) })
    const starting = context.client.start()
    await until(() => context.states.length > 0)

    await context.client.stop()
    release()
    await starting

    expect([context.states, context.relay.handles]).toEqual([[{ kind: 'connecting' }], []])
  })

  it('should connect once when the user retries before the retry time', async () => {
    const context = await setup({ retryDelay: () => SHORT_DELAY_MS, fetchHandle: failingFirstHandle() })
    await context.client.start()

    context.client.retryNow()
    await until(() => context.relay.handles.length > 0)
    await new Promise((resolve) => setTimeout(resolve, SHORT_DELAY_MS * 2))

    expect(context.relay.handles).toEqual(['handle-two'])
  })

  it('should not connect when the network returns before the pairing loads', async () => {
    const context = await setup()

    context.client.pauseForNetwork()
    context.client.resumeForNetwork()
    await flush()

    expect(context.counts.handles).toBe(0)
  })

  it('should not open a second connection when the user retries while connected', async () => {
    const context = await ready()

    context.client.retryNow()
    await flush()

    expect(context.relay.handles).toEqual(['handle-1'])
  })

  it('should count each failed attempt when picking the next delay', async () => {
    const counts: number[] = []
    const context = await setup({
      retryDelay: (count) => (counts.push(count), counts.length < 2 ? SHORT_DELAY_MS : LONG_DELAY_MS),
      fetchHandle: async () => Promise.reject(new RelayUnreachableError('offline')),
    })

    await context.client.start()
    await until(() => counts.length === 2)

    expect(counts).toEqual([0, 1])
  })

  it('should refuse to send before a session exists', async () => {
    const context = await setup()

    await expect(context.client.send(payloadFixtures['run.start'] as Payload)).rejects.toThrow('session is not ready')
  })
})
