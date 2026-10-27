import { afterEach, describe, expect, it, vi } from 'vitest'
import { encodeBase64Url } from '@/features/relay/base64url'
import { PAIR_CONFIRM_TIMEOUT_SECONDS, STEP_TIMEOUT_SECONDS } from '@/features/relay/contract/limits'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { CvUpload, Payload } from '@/features/relay/contract/payload.gen'
import { splitMessage } from '@/features/relay/chunks'
import { generateWebKeys } from '@/features/relay/keyStore'
import type { PaceClock } from '@/features/relay/sendPacer'
import { FrameError } from '@/features/relay/session'
import { RESTART_BACKOFF_MS, SessionRunner, type SessionEvent } from '@/features/relay/sessionRunner'
import { CliPeer, FakeRelay, helloText, type PeerOptions, TEST_CHANNEL } from '@/test/fakeRelay'
import { drainTurns, flush, until } from '@/test/flush'

const STATUS = JSON.stringify(payloadFixtures.status)
const UNPAIR: Payload = { type: 'unpair', v: 1, id: '00000000-0000-4000-8000-000000000001' }
const MS = 1000
const MEGABYTE = 1024 * 1024
const LARGE_UPLOAD_BYTES = 100 * 1024
const KEY_BYTES = 32

interface Options {
  paired?: boolean
  limitRate?: boolean
  peer?: PeerOptions
  parseAssembled?: (text: string) => Promise<Payload | undefined>
  paceClock?: PaceClock
}

const PAIR_CODE = 'KQFB-MXTW'

type Context = Awaited<ReturnType<typeof setup>>

async function setup({ paired = false, limitRate = false, peer: peerOptions = {}, parseAssembled, paceClock }: Options = {}) {
  const relay = new FakeRelay()
  relay.limitRate = limitRate
  const peer = await CliPeer.attach(relay, peerOptions)
  const events: SessionEvent[] = []
  const runner = new SessionRunner({
    channel: TEST_CHANNEL,
    webStatic: await generateWebKeys(),
    agent: 'Firefox on macOS',
    cliStatic: paired ? peer.cli.staticPublic : undefined,
    pairCode: paired ? undefined : PAIR_CODE,
    onEvent: (event) => events.push(event),
    paceClock: paceClock ?? relay.clock,
    ...(parseAssembled && { parseAssembled }),
  })
  await runner.start(relay.connect, 'handle-one')
  return { relay, peer, runner, events, kinds: () => events.map((event) => event.kind) }
}

function largeUpload(bytes = LARGE_UPLOAD_BYTES): CvUpload {
  return { ...(payloadFixtures['cv.upload'] as CvUpload), content_b64: 'A'.repeat(bytes) }
}

async function nextEvent(context: Context, act: () => unknown): Promise<void> {
  const before = context.events.length
  await act()
  await until(() => context.events.length > before)
}

async function online(context: Context): Promise<void> {
  context.relay.presence(true)
  await until(() => context.events.some((event) => event.kind !== 'handshaking'))
}

async function pairedReady() {
  const context = await setup({ paired: true })
  await online(context)
  return context
}

function expectBrokenAbort(context: Context): void {
  expect([context.relay.steps().at(-1), context.events.at(-1)]).toEqual(['abort', { kind: 'closed', reason: 'broken' }])
}

async function commitSent(): Promise<Context> {
  const context = await setup({ paired: true, peer: { replyToCommit: false } })
  context.relay.presence(true)
  await until(() => context.relay.steps().length === 1)
  return context
}

async function wordsShown(options: Options = {}) {
  const context = await setup(options)
  await online(context)
  return context
}

async function answeredYes() {
  const context = await wordsShown()
  context.runner.answerWords(true)
  await until(() => context.peer.browserConfirmed)
  return context
}

async function pairingVerified() {
  const context = await answeredYes()
  await nextEvent(context, () => context.peer.sendData(STATUS))
  await until(() => context.events.at(-1)?.kind === 'message')
  return context
}

afterEach(() => {
  vi.useRealTimers()
})

describe('SessionRunner pair tag', () => {
  it('should pair with a CLI whose current code is the claimed code', async () => {
    const context = await wordsShown({ peer: { pairCode: PAIR_CODE } })

    expect(context.events.at(-1)?.kind).toBe('confirming')
  })

  it('should get no offer from a CLI that moved on to a newer code', async () => {
    const context = await setup({ peer: { pairCode: 'BCDF-GHJK' } })

    context.relay.presence(true)
    await until(() => context.peer.commits.length === 1)
    await flush()

    expect([context.relay.steps(), context.kinds()]).toEqual([['commit'], ['handshaking']])
  })

  it('should send no pair tag in a paired commit', async () => {
    const context = await pairedReady()

    expect(context.peer.commits.map((step) => 'pair_tag' in step)).toEqual([false])
  })

  it('should send no pair tag when a paired runner is given a code', async () => {
    const relay = new FakeRelay()
    const peer = await CliPeer.attach(relay, { replyToCommit: false })
    const options = { channel: TEST_CHANNEL, webStatic: await generateWebKeys(), agent: 'a', cliStatic: peer.cli.staticPublic, pairCode: PAIR_CODE }
    await new SessionRunner({ ...options, onEvent: () => undefined }).start(relay.connect, 'handle-one')

    relay.presence(true)
    await until(() => peer.commits.length === 1)

    expect('pair_tag' in (peer.commits[0] ?? {})).toBe(false)
  })

  it('should refuse to start a pairing runner without the claimed code', async () => {
    const options = { channel: TEST_CHANNEL, webStatic: await generateWebKeys(), agent: 'a', cliStatic: undefined, pairCode: undefined, onEvent: () => undefined }

    expect(() => new SessionRunner(options)).toThrow('pairing needs the claimed code')
  })
})

describe('SessionRunner pairing', () => {
  it('should send nothing before the CLI is online', async () => {
    const { relay } = await setup()

    await flush()

    expect(relay.sent).toEqual([])
  })

  it('should show the CLI words once the CLI confirm verifies', async () => {
    const { peer, events } = await wordsShown()

    expect(events).toContainEqual({ kind: 'confirming', words: peer.cli.words })
  })

  it('should hold the browser confirm until the website Yes', async () => {
    const { relay, kinds } = await wordsShown()

    await flush()

    expect([relay.steps(), kinds().includes('ready')]).toEqual([['commit', 'reveal'], false])
  })

  it('should report ready with the CLI key only after the first CLI data frame verifies', async () => {
    const context = await answeredYes()
    const { peer, events } = context
    const beforeFrame = events.some((event) => event.kind === 'ready')

    await nextEvent(context, () => peer.sendData(STATUS))
    await until(() => events.at(-1)?.kind === 'message')

    expect([beforeFrame, peer.browserConfirmed, events.at(-2)]).toEqual([false, true, { kind: 'ready', cliStatic: peer.cli.staticPublic }])
  })

  it('should pass the first CLI message on after ready', async () => {
    const { events } = await pairingVerified()

    expect(events.at(-1)).toEqual({ kind: 'message', payload: payloadFixtures.status })
  })

  it('should close as rejected with an abort when the website says No', async () => {
    const context = await wordsShown()

    await nextEvent(context, () => context.runner.answerWords(false))

    expect([context.relay.steps().at(-1), context.events.at(-1)]).toEqual(['abort', { kind: 'closed', reason: 'rejected' }])
  })

  it('should close as refused without a CLI key when the terminal aborts after Yes', async () => {
    const context = await answeredYes()

    await nextEvent(context, () => context.peer.sendHello({ t: 'abort' }))

    expect([context.events.some((event) => event.kind === 'ready'), context.events.at(-1)]).toEqual([false, { kind: 'closed', reason: 'abort' }])
  })

  it('should close when a CLI data frame arrives before the website Yes', async () => {
    const context = await wordsShown()

    await nextEvent(context, () => context.peer.sendData(STATUS))

    expectBrokenAbort(context)
  })

  it('should close when the relay drops before the first CLI data frame', async () => {
    const context = await answeredYes()

    await nextEvent(context, () => context.relay.drop())

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'connection' })
  })

  it('should close when the CLI goes offline during the word compare', async () => {
    const context = await wordsShown()

    await nextEvent(context, () => context.relay.presence(false))

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'offline' })
  })

  it('should send nothing to a CLI that went offline', async () => {
    const context = await wordsShown()
    const sentBefore = context.relay.sent.length

    await nextEvent(context, () => context.relay.presence(false))

    expect(context.relay.sent).toHaveLength(sentBefore)
  })

  it('should keep waiting past STEP_TIMEOUT_SECONDS while the words show', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { events, relay } = await wordsShown()

    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS - 1)
    await flush()

    expect([events.at(-1)?.kind, relay.steps().at(-1)]).toEqual(['confirming', 'reveal'])
  })

  it('should abort when no first CLI data frame comes within PAIR_CONFIRM_TIMEOUT_SECONDS of the words', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await answeredYes()

    await nextEvent(context, () => vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS))

    expect([context.relay.steps().at(-1), context.events.at(-1)]).toEqual(['abort', { kind: 'closed', reason: 'words_timeout' }])
  })

  it('should close as no_answer when the CLI never comes online within PAIR_CONFIRM_TIMEOUT_SECONDS of the claim', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup()

    await nextEvent(context, () => vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS))

    expect([context.relay.sent, context.events.at(-1)]).toEqual([[], { kind: 'closed', reason: 'no_answer' }])
  })

  it('should keep waiting for the CLI just inside PAIR_CONFIRM_TIMEOUT_SECONDS of the claim', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup()

    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS - 1)
    await flush()

    expect(context.events).toEqual([])
  })

  it('should never send a commit after the claim timed out when the CLI comes online later', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup()
    await nextEvent(context, () => vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS))

    context.relay.presence(true)
    await flush()

    expect([context.relay.sent, context.relay.stopped]).toEqual([[], true])
  })

  it('should abort the commit when the offer does not come within PAIR_CONFIRM_TIMEOUT_SECONDS of the claim', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup({ peer: { replyToCommit: false } })
    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS - STEP_TIMEOUT_SECONDS * MS + MS)
    context.relay.presence(true)
    await until(() => context.relay.steps().length === 1)

    await nextEvent(context, () => vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS - MS))

    expect([context.relay.steps(), context.events.at(-1)]).toEqual([['commit', 'abort'], { kind: 'closed', reason: 'no_answer' }])
  })

  it('should ignore the claim deadline once the offer arrives', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup()
    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS - MS)
    await online(context)

    vi.advanceTimersByTime(2 * MS)
    await flush()

    expect(context.events.at(-1)?.kind).toBe('confirming')
  })

  it('should stop the relay when a pairing ends', async () => {
    const context = await wordsShown()

    await nextEvent(context, () => context.peer.sendHello({ t: 'abort' }))

    await until(() => context.relay.stopped)
    expect(context.relay.stopped).toBe(true)
  })

  it('should abort when no offer comes within STEP_TIMEOUT_SECONDS of the commit', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup({ peer: { replyToCommit: false } })
    context.relay.presence(true)
    await until(() => context.relay.steps().length === 1)

    await nextEvent(context, () => vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS))

    expect([context.relay.steps(), context.events.at(-1)]).toEqual([['commit', 'abort'], { kind: 'closed', reason: 'timeout' }])
  })

  it('should not restart a handshake after a pairing closes', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await wordsShown()
    await nextEvent(context, () => context.peer.sendHello({ t: 'abort' }))

    vi.advanceTimersByTime(RESTART_BACKOFF_MS.at(-1) ?? 0)
    await flush()

    expect(context.relay.steps().filter((step) => step === 'commit')).toHaveLength(1)
  })

  it('should fail the handshake when the CLI MAC is tampered', async () => {
    const { events, relay } = await wordsShown({ peer: { confirmMac: (mac) => mac.map((byte) => byte ^ 1) } })

    expect([relay.steps().at(-1), events.at(-1)]).toEqual(['abort', { kind: 'failed', reason: 'handshake' }])
  })
})

describe('SessionRunner receiver rules', () => {
  it('should ignore frames for another sid without an answer', async () => {
    const { peer, relay, events } = await wordsShown()
    const sentBefore = relay.sent.length

    peer.sendHello({ t: 'abort' }, encodeBase64Url(new Uint8Array(16).fill(9)))
    await flush()

    expect([relay.sent.length, events.at(-1)?.kind]).toEqual([sentBefore, 'confirming'])
  })

  it('should ignore frames for another channel', async () => {
    const { peer, relay, events } = await wordsShown()

    relay.deliver({ ...peer.hello({ t: 'abort' }), channel: TEST_CHANNEL.replace('Q', 'R') })
    await flush()

    expect(events.at(-1)?.kind).toBe('confirming')
  })

  it.each([
    { rule: 'a repeated key', step: () => '{"t":"abort","t":"abort"}' },
    { rule: 'an out-of-order step', step: () => JSON.stringify({ t: 'offer', cli_static: encodeBase64Url(new Uint8Array(32)), cli_eph: encodeBase64Url(new Uint8Array(32)) }) },
  ])('should abort the handshake on $rule', async ({ step }) => {
    const context = await wordsShown()

    await nextEvent(context, () => context.relay.deliver({ ...context.peer.hello({}), ciphertext: encodeBase64Url(new TextEncoder().encode(step())) }))

    expectBrokenAbort(context)
  })

  it('should close on an envelope that breaks the contract', async () => {
    const context = await wordsShown()

    await nextEvent(context, () =>
      context.relay.deliver(`{"v":1,"channel":"${TEST_CHANNEL}","sid":"x","seq":0,"kind":"hello","ciphertext":"${helloText({ t: 'abort' })}","extra":1}`),
    )

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'broken' })
  })

  it('should close the session on the first rejected data frame', async () => {
    const context = await pairingVerified()
    await nextEvent(context, () => context.peer.sendData(STATUS, 3))

    await context.peer.sendData(STATUS, 2)
    await flush()

    expect(context.events.slice(-1)).toEqual([{ kind: 'closed', reason: 'broken' }])
  })

  it('should report no ready when the first CLI data frame is not a contract payload', async () => {
    const context = await answeredYes()

    await nextEvent(context, () => context.peer.sendData('{"type":"status"}'))
    await until(() => context.events.at(-1)?.kind === 'closed')

    expect(context.kinds()).not.toContain('ready')
  })

  it('should close the session on a data frame that is not a contract payload', async () => {
    const context = await pairingVerified()

    await nextEvent(context, () => context.peer.sendData('{"type":"status"}'))

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'broken' })
  })
})

describe('SessionRunner paired', () => {
  it('should seal no queued frame after the CLI goes offline', async () => {
    const context = await pairedReady()
    let release = (): void => undefined
    context.relay.sendGate = new Promise((resolve) => {
      release = resolve
    })
    const first = context.runner.send(UNPAIR)
    const second = context.runner.send(UNPAIR)
    await nextEvent(context, () => context.relay.presence(false))

    release()

    await first
    await expect(second).rejects.toThrow(new FrameError('closed'))
    expect(context.peer.received).toHaveLength(1)
  })

  it('should be ready after the CLI confirm without a word compare', async () => {
    const { kinds, peer } = await pairedReady()

    await until(() => peer.browserConfirmed)

    expect([kinds(), peer.browserConfirmed]).toEqual([['handshaking', 'ready'], true])
  })

  it('should fail with cli_key when the offer CLI key differs from the stored one', async () => {
    const relay = new FakeRelay()
    await CliPeer.attach(relay)
    const events: SessionEvent[] = []
    const runner = new SessionRunner({
      channel: TEST_CHANNEL,
      webStatic: await generateWebKeys(),
      agent: 'Firefox on macOS',
      cliStatic: new Uint8Array(32).fill(5),
      onEvent: (event) => events.push(event),
    })
    await runner.start(relay.connect, 'handle-one')

    relay.presence(true)
    await until(() => events.at(-1)?.kind === 'failed')

    expect([relay.steps(), events.at(-1)]).toEqual([['commit', 'abort'], { kind: 'failed', reason: 'cli_key' }])
  })

  it('should stop without a restart on unknown_device', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await commitSent()
    await nextEvent(context, () => context.peer.sendHello({ t: 'unknown_device' }, context.relay.sids()[0]))

    vi.advanceTimersByTime(RESTART_BACKOFF_MS.at(-1) ?? 0)
    await flush()

    expect([context.relay.steps(), context.events.at(-1)]).toEqual([['commit'], { kind: 'failed', reason: 'unknown_device' }])
  })

  it('should restart with a new sid only after the backoff delay when the session is closed by abort', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await pairedReady()
    await nextEvent(context, () => context.peer.sendHello({ t: 'abort' }))
    vi.advanceTimersByTime((RESTART_BACKOFF_MS[0] ?? 0) - 1)
    await flush()
    const commitsBefore = context.relay.steps().filter((step) => step === 'commit').length

    vi.advanceTimersByTime(1)
    await until(() => context.relay.sids().length === 2)

    expect([commitsBefore, context.relay.sids().length]).toEqual([1, 2])
  })

  it('should close without an answer on unknown_session', async () => {
    const context = await pairedReady()
    const sentBefore = context.relay.sent.length

    await nextEvent(context, () => context.peer.sendHello({ t: 'unknown_session' }))

    expect([context.relay.sent.length, context.events.at(-1)]).toEqual([sentBefore, { kind: 'closed', reason: 'unknown_session' }])
  })

  it('should restart after unknown_session as after abort', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await pairedReady()

    await nextEvent(context, () => context.peer.sendHello({ t: 'unknown_session' }))
    vi.advanceTimersByTime(RESTART_BACKOFF_MS[0] ?? 0)
    await until(() => context.events.at(-1)?.kind === 'ready')

    expect(context.relay.sids()).toHaveLength(2)
  })

  it('should keep a paired runner waiting for an offline CLI past PAIR_CONFIRM_TIMEOUT_SECONDS', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup({ paired: true })

    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS)
    await flush()

    expect([context.events, context.relay.stopped]).toEqual([[], false])
  })

  it('should keep restarting when every handshake reaches ready', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await pairedReady()

    for (let round = 0; round <= RESTART_BACKOFF_MS.length; round += 1) {
      const sids = context.relay.sids().length
      context.peer.sendHello({ t: 'abort' })
      await until(() => context.events.at(-1)?.kind === 'closed')
      vi.advanceTimersByTime(RESTART_BACKOFF_MS[0] ?? 0)
      await until(() => context.relay.sids().length > sids && context.events.at(-1)?.kind === 'ready')
    }

    expect([context.relay.sids().length, context.kinds().includes('failed')]).toEqual([RESTART_BACKOFF_MS.length + 2, false])
  })

  it('should give up after every backoff step fails in a row', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await commitSent()
    for (const [round, delay] of RESTART_BACKOFF_MS.entries()) {
      await nextEvent(context, () => vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS))
      vi.advanceTimersByTime(delay)
      await until(() => context.relay.sids().length === round + 2)
    }

    vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS)
    await until(() => context.events.at(-1)?.kind === 'failed')

    expect([context.relay.sids().length, context.events.at(-1), context.relay.stopped]).toEqual([RESTART_BACKOFF_MS.length + 1, { kind: 'failed', reason: 'handshake' }, true])
  })

  it('should report no timeout when the CLI goes offline as the step timer fires', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await commitSent()

    context.relay.presence(false)
    vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS)
    await drainTurns()

    expect(context.events.at(-1)).toEqual({ kind: 'offline' })
  })

  it('should reset the restart budget when the CLI comes back online', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup({ paired: true, peer: { replyToCommit: false } })
    const failHandshake = async (): Promise<void> => {
      const sids = context.relay.sids().length
      await nextEvent(context, () => vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS))
      vi.advanceTimersByTime(RESTART_BACKOFF_MS.at(-1) ?? 0)
      await until(() => context.relay.sids().length > sids || context.events.at(-1)?.kind === 'failed')
    }
    context.relay.presence(true)
    await until(() => context.relay.steps().length === 1)
    for (let round = 1; round < RESTART_BACKOFF_MS.length; round += 1) await failHandshake()
    await nextEvent(context, () => context.relay.presence(false))
    const sidsOffline = context.relay.sids().length
    context.relay.presence(true)
    await until(() => context.relay.sids().length > sidsOffline)

    await failHandshake()
    await failHandshake()

    expect(context.kinds().includes('failed')).toBe(false)
  })

  it('should start a new handshake when the CLI comes back online', async () => {
    const context = await pairedReady()
    await nextEvent(context, () => context.relay.presence(false))

    context.relay.presence(true)
    await until(() => context.events.at(-1)?.kind === 'ready')

    expect([context.relay.sids().length, context.kinds().slice(-3)]).toEqual([2, ['offline', 'handshaking', 'ready']])
  })

  it('should hold the next frame until the previous send finishes', async () => {
    const { relay, peer, runner } = await pairedReady()
    let release = (): void => undefined
    relay.sendGate = new Promise((resolve) => {
      release = resolve
    })
    const before = relay.sendCalls
    const other: Payload = { ...UNPAIR, id: '00000000-0000-4000-8000-000000000002' }
    const sends = [runner.send(UNPAIR), runner.send(other)]
    await until(() => relay.sendCalls > before)
    await flush()
    const callsWhileHeld = relay.sendCalls - before

    release()
    await Promise.all(sends)

    expect([callsWhileHeld, peer.received.map((text) => JSON.parse(text))]).toEqual([1, [UNPAIR, other]])
  })

  it('should still send the next frame after a failed send', async () => {
    const { relay, peer, runner } = await pairedReady()
    relay.failNextSend = true
    const failed = runner.send(UNPAIR)
    const next = runner.send(UNPAIR)

    await expect(failed).rejects.toThrow('relay refused the send')
    await next

    expect(peer.received).toHaveLength(1)
  })

  it('should send a payload the CLI opens', async () => {
    const { peer, runner } = await pairedReady()

    await runner.send(UNPAIR)

    expect(peer.received.map((text) => JSON.parse(text))).toEqual([UNPAIR])
  })

  it('should send a 100 KB payload as chunk frames', async () => {
    const { peer, runner } = await pairedReady()
    await runner.send(largeUpload())

    expect(peer.received.map((text) => (JSON.parse(text) as Payload).type)).toEqual(['chunk', 'chunk', 'chunk', 'chunk'])
  })

  it('should report each chunk once it is written', async () => {
    const { runner } = await pairedReady()
    const progress: number[][] = []

    await runner.send(largeUpload(), (sent, count) => progress.push([sent, count]))

    expect(progress).toEqual([
      [1, 4],
      [2, 4],
      [3, 4],
      [4, 4],
    ])
  })

  it('should send a 1 MB upload to the end under the relay rate limits', async () => {
    const context = await setup({ paired: true, limitRate: true })
    await online(context)
    const upload = largeUpload(MEGABYTE)
    const count = splitMessage(upload).length
    const progress: number[][] = []

    await context.runner.send(upload, (sent, total) => progress.push([sent, total]))

    expect(progress.at(-1)).toEqual([count, count])
  })

  it('should hold the next frame while the pacer waits', async () => {
    let isWaiting = false
    const heldClock: PaceClock = {
      now: () => 0,
      wait: () => {
        isWaiting = true
        return new Promise(() => undefined)
      },
    }
    const context = await setup({ paired: true, paceClock: heldClock })
    await online(context)
    void context.runner.send(largeUpload(MEGABYTE)).catch(() => undefined)
    await until(() => isWaiting)
    const sentAtWait = context.relay.sendCalls

    await flush()

    expect(context.relay.sendCalls).toBe(sentAtWait)
  })

  it('should report no progress for a chunk the relay refused', async () => {
    const { relay, runner } = await pairedReady()
    const progress: number[][] = []
    relay.failNextSend = true

    await expect(runner.send(largeUpload(), (sent, count) => progress.push([sent, count]))).rejects.toThrow('relay refused the send')

    expect(progress).toEqual([])
  })

  it('should stop reporting progress once the runner stops', async () => {
    const { relay, runner } = await pairedReady()
    const progress: number[][] = []
    let release = (): void => undefined
    relay.sendGate = new Promise((resolve) => {
      release = resolve
    })
    const before = relay.sendCalls
    const sending = runner.send(largeUpload(), (sent, count) => progress.push([sent, count])).catch(() => undefined)
    await until(() => relay.sendCalls > before)

    const stopping = runner.stop()
    await flush()
    release()
    await Promise.all([sending, stopping])

    expect(progress).toEqual([])
  })

  it('should refuse to send before the session is ready', async () => {
    const { runner } = await wordsShown()

    await expect(runner.send(UNPAIR)).rejects.toThrow('session is not ready')
  })

  it('should close the session with an abort on stop', async () => {
    const { relay, runner } = await pairedReady()

    await runner.stop()

    expect([relay.steps().at(-1), relay.stopped]).toEqual(['abort', true])
  })
})

describe('SessionRunner lifecycle', () => {
  it('should start the handshake when the CLI is online before the connection resolves', async () => {
    const relay = new FakeRelay()
    relay.onlineAtConnect = true
    await CliPeer.attach(relay)
    const events: SessionEvent[] = []
    const runner = new SessionRunner({ channel: TEST_CHANNEL, webStatic: await generateWebKeys(), agent: 'a', cliStatic: undefined, pairCode: PAIR_CODE, onEvent: (event) => events.push(event) })

    await runner.start(relay.connect, 'handle-one')

    await until(() => events.at(-1)?.kind === 'confirming')
    expect(relay.steps()).toEqual(['commit', 'reveal'])
  })

  it('should keep the handshake when presence repeats with the CLI online', async () => {
    const context = await wordsShown()

    context.relay.presence(true)
    await flush()

    expect([context.relay.sids().length, context.events.at(-1)?.kind]).toEqual([1, 'confirming'])
  })

  it('should report offline once when presence repeats with the CLI offline', async () => {
    const context = await pairedReady()

    context.relay.presence(false)
    context.relay.presence(false)
    await drainTurns()

    expect(context.kinds().filter((kind) => kind === 'offline')).toHaveLength(1)
  })

  it('should report nothing more when the relay drops after a pairing ended', async () => {
    const context = await wordsShown()
    await nextEvent(context, () => context.runner.answerWords(false))
    const count = context.events.length

    context.relay.drop()
    context.relay.presence(false)
    await drainTurns()

    expect(context.events).toHaveLength(count)
  })

  it('should report nothing when the relay drops after stop', async () => {
    const context = await pairedReady()
    await context.runner.stop()
    const count = context.events.length

    context.relay.drop()
    await drainTurns()

    expect(context.events).toHaveLength(count)
  })

  it('should not start again after a failure when the CLI comes back', async () => {
    const relay = new FakeRelay()
    await CliPeer.attach(relay)
    const events: SessionEvent[] = []
    const runner = new SessionRunner({ channel: TEST_CHANNEL, webStatic: await generateWebKeys(), agent: 'a', cliStatic: new Uint8Array(32).fill(5), onEvent: (event) => events.push(event) })
    await runner.start(relay.connect, 'handle-one')
    relay.presence(true)
    await until(() => events.at(-1)?.kind === 'failed')

    relay.presence(false)
    relay.presence(true)
    await flush()

    expect(relay.steps().filter((step) => step === 'commit')).toHaveLength(1)
  })

  it('should ignore a second Yes', async () => {
    const context = await answeredYes()

    context.runner.answerWords(true)
    await flush()

    expect([context.relay.steps(), context.events.at(-1)?.kind]).toEqual([['commit', 'reveal', 'confirm'], 'confirming'])
  })

  it('should stop the relay when the abort on stop cannot be sent', async () => {
    const { relay, runner } = await pairedReady()
    relay.failSends = true

    await runner.stop()

    expect(relay.stopped).toBe(true)
  })

  it('should send no abort on stop before a handshake', async () => {
    const { relay, runner } = await setup()

    await runner.stop()

    expect([relay.sent, relay.stopped]).toEqual([[], true])
  })

  it('should not start a handshake after stop', async () => {
    const { relay, runner } = await setup()
    await runner.stop()

    relay.presence(true)
    await flush()

    expect(relay.sent).toEqual([])
  })

  it('should ignore a broken envelope while no handshake runs', async () => {
    const { relay, events } = await setup()

    relay.deliver('{"v":1}')
    await drainTurns()

    expect([relay.sent, events]).toEqual([[], []])
  })

  it('should close when the relay refuses a send', async () => {
    const context = await setup()
    context.relay.failSends = true

    context.relay.presence(true)
    await until(() => context.events.at(-1)?.kind === 'closed')

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'broken' })
  })
})

describe('SessionRunner handshake rules', () => {
  it('should close as broken on unknown_device during the word compare', async () => {
    const context = await wordsShown()

    await nextEvent(context, () => context.peer.sendHello({ t: 'unknown_device' }))

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'broken' })
  })

  it('should close as broken when an offer key gives an all-zero secret', async () => {
    const context = await setup({ peer: { replyToCommit: false } })
    context.relay.presence(true)
    await until(() => context.relay.steps().length === 1)
    const zero = encodeBase64Url(new Uint8Array(32))

    await nextEvent(context, () => context.peer.sendHello({ t: 'offer', cli_static: zero, cli_eph: zero }, context.relay.sids()[0]))

    expectBrokenAbort(context)
  })

  it('should close as broken on a confirm before the offer', async () => {
    const context = await commitSent()

    await nextEvent(context, () => context.peer.sendHello({ t: 'confirm', mac: encodeBase64Url(new Uint8Array(KEY_BYTES)) }, context.relay.sids()[0]))

    expectBrokenAbort(context)
  })

  it('should close as broken on a second offer after the reveal', async () => {
    const context = await setup({ peer: { replyToReveal: false } })
    context.relay.presence(true)
    await until(() => context.relay.steps().length === 2)

    await nextEvent(context, () => context.peer.sendHello({ t: 'offer', cli_static: encodeBase64Url(new Uint8Array(KEY_BYTES)), cli_eph: encodeBase64Url(new Uint8Array(KEY_BYTES)) }))

    expectBrokenAbort(context)
  })

  it('should abort when no CLI confirm comes within STEP_TIMEOUT_SECONDS of the reveal', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await setup({ peer: { replyToReveal: false } })
    context.relay.presence(true)
    await until(() => context.relay.steps().length === 2)

    await nextEvent(context, () => vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS))

    expect([context.relay.steps(), context.events.at(-1)]).toEqual([['commit', 'reveal', 'abort'], { kind: 'closed', reason: 'timeout' }])
  })

  it('should stay ready past STEP_TIMEOUT_SECONDS after a paired handshake', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await pairedReady()

    vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS)
    await flush()

    expect(context.events.at(-1)?.kind).toBe('ready')
  })

  it('should stay ready past PAIR_CONFIRM_TIMEOUT_SECONDS after the first CLI frame', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const context = await pairingVerified()

    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS)
    await flush()

    expect(context.events.at(-1)?.kind).toBe('message')
  })

  it('should report ready once for many CLI frames', async () => {
    const context = await pairingVerified()

    await nextEvent(context, () => context.peer.sendData(STATUS))

    expect(context.kinds().filter((kind) => kind === 'ready')).toHaveLength(1)
  })

  it('should send abort when a data frame is rejected', async () => {
    const context = await pairingVerified()

    await nextEvent(context, () => context.peer.sendData(STATUS, 3))

    expect(context.relay.steps().at(-1)).toBe('abort')
  })

  it('should send abort when an envelope breaks the contract', async () => {
    const context = await wordsShown()

    await nextEvent(context, () => context.relay.deliver('{"v":1}'))

    expect(context.relay.steps().at(-1)).toBe('abort')
  })

  it('should close on a data frame that is not UTF-8', async () => {
    const context = await pairingVerified()
    const bytes = new TextEncoder().encode(STATUS.replace('laptop', 'Xaptop'))
    bytes[bytes.indexOf(0x58)] = 0xff

    await nextEvent(context, () => context.peer.sendData(bytes))

    expect(context.events.at(-1)).toEqual({ kind: 'closed', reason: 'broken' })
  })

  it('should pass on a chunked CLI message once whole', async () => {
    const context = await pairingVerified()
    const parts = splitMessage(largeUpload())
    const count = context.events.length

    for (const part of parts) await context.peer.sendData(JSON.stringify(part))
    await until(() => context.events.length > count)
    await flush()

    expect(context.events.slice(count).map((event) => (event.kind === 'message' ? event.payload.type : event.kind))).toEqual(['cv.upload'])
  })

  it('should close with an abort when the chunk parser fails', async () => {
    const context = await setup({ paired: true, parseAssembled: async () => Promise.reject(new Error('parse failed')) })
    await online(context)
    const parts = splitMessage(largeUpload())

    for (const part of parts) await context.peer.sendData(JSON.stringify(part))
    await until(() => context.events.at(-1)?.kind === 'closed')

    expectBrokenAbort(context)
  })

  it('should parse a chunked CLI message with the given parser', async () => {
    const texts: string[] = []
    const parsed = payloadFixtures.status as Payload
    const context = await setup({ paired: true, parseAssembled: async (text) => (texts.push(text), parsed) })
    await online(context)
    const message = largeUpload()
    const count = context.events.length

    for (const part of splitMessage(message)) await context.peer.sendData(JSON.stringify(part))
    await until(() => context.events.length > count)

    expect([texts, context.events.at(-1)]).toEqual([[JSON.stringify(message)], { kind: 'message', payload: parsed }])
  })
})
