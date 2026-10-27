import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { COMMAND_TIMEOUT_MS, type Command, CommandError, CommandTracker } from '@/features/relay/commands'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { AdMark, ErrorMessage, RunStart, Saved } from '@/features/relay/contract/payload.gen'

function draftOf<Message extends Command>(command: Message): Omit<Message, 'id'> {
  return Object.fromEntries(Object.entries(command).filter(([key]) => key !== 'id')) as Omit<Message, 'id'>
}

const SAVED = payloadFixtures.saved as Saved
const ERROR = payloadFixtures.error as ErrorMessage
const RUN_START = draftOf(payloadFixtures['run.start'] as RunStart)
const AD_MARK = draftOf(payloadFixtures['ad.mark'] as AdMark)
const SLOW_DELIVERY_MS = 20_000

type Outcome = { saved: Saved } | { error: CommandError } | 'pending'

function track(promise: Promise<Saved>): () => Outcome {
  let outcome: Outcome = 'pending'
  promise.then(
    (saved) => {
      outcome = { saved }
    },
    (error: CommandError) => {
      outcome = { error }
    },
  )
  return () => outcome
}

function setup(deliver: (command: Command) => Promise<void> = async () => undefined) {
  const delivered: Command[] = []
  const tracker = new CommandTracker(async (command) => {
    delivered.push(command)
    await deliver(command)
  })
  return { tracker, delivered }
}

function idOf(command: Command | undefined): string {
  return command?.id ?? ''
}

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

describe('CommandTracker', () => {
  it('should resolve only the command whose id matches the saved ref', async () => {
    const { tracker, delivered } = setup()
    const first = track(tracker.send(RUN_START))
    const second = track(tracker.send(AD_MARK))
    await vi.advanceTimersByTimeAsync(0)

    tracker.settle({ ...SAVED, ref: idOf(delivered[1]) })
    await vi.advanceTimersByTimeAsync(0)

    expect([first(), second(), delivered]).toEqual([
      'pending',
      { saved: { ...SAVED, ref: idOf(delivered[1]) } },
      [
        { ...RUN_START, id: expect.any(String) },
        { ...AD_MARK, id: expect.not.stringMatching(idOf(delivered[0])) },
      ],
    ])
  })

  it('should reject with the code and message of an error reply', async () => {
    const { tracker, delivered } = setup()
    const outcome = track(tracker.send(RUN_START))
    await vi.advanceTimersByTimeAsync(0)

    tracker.settle({ ...ERROR, ref: idOf(delivered[0]) })
    await vi.advanceTimersByTimeAsync(0)

    expect(outcome()).toEqual({ error: new CommandError(ERROR.code, ERROR.message, ERROR.field) })
  })

  it('should reject with timeout only after 15 seconds without a reply', async () => {
    const { tracker } = setup()
    const outcome = track(tracker.send(RUN_START))

    await vi.advanceTimersByTimeAsync(COMMAND_TIMEOUT_MS - 1)
    const before = outcome()
    await vi.advanceTimersByTimeAsync(1)

    expect([before, outcome()]).toEqual(['pending', { error: new CommandError('timeout', 'no answer from your machine.') }])
  })

  it('should count the timeout from the end of a slow delivery', async () => {
    const { tracker } = setup(() => new Promise((resolve) => setTimeout(resolve, SLOW_DELIVERY_MS)))
    const outcome = track(tracker.send(RUN_START))

    await vi.advanceTimersByTimeAsync(SLOW_DELIVERY_MS + COMMAND_TIMEOUT_MS - 1)

    expect(outcome()).toBe('pending')
  })

  it('should reject as disconnected when the delivery fails', async () => {
    const { tracker } = setup(async () => Promise.reject(new Error('session is not ready')))
    const outcome = track(tracker.send(RUN_START))

    await vi.advanceTimersByTimeAsync(0)

    expect(outcome()).toEqual({ error: new CommandError('disconnected', 'connection lost.') })
  })

  it.each([SAVED, ERROR])('should ignore a late $type reply', (reply) => {
    const { tracker } = setup()

    expect(() => tracker.settle({ ...reply, ref: 'late-reply' })).not.toThrow()
  })

  it('should resolve a reply that arrives before its delivery finishes', async () => {
    let finishDelivery: () => void = () => undefined
    const { tracker, delivered } = setup(() => new Promise<void>((resolve) => (finishDelivery = resolve)))
    const outcome = track(tracker.send(RUN_START))

    tracker.settle({ ...SAVED, ref: idOf(delivered[0]) })
    finishDelivery()
    await vi.advanceTimersByTimeAsync(COMMAND_TIMEOUT_MS)

    expect(outcome()).toEqual({ saved: { ...SAVED, ref: idOf(delivered[0]) } })
  })

  it('should reject once when the connection drops during a failing delivery', async () => {
    let failDelivery: () => void = () => undefined
    const { tracker } = setup(() => new Promise<void>((_, reject) => (failDelivery = () => reject(new Error('socket closed')))))
    const outcome = track(tracker.send(RUN_START))

    tracker.failAll()
    failDelivery()
    await vi.advanceTimersByTimeAsync(0)

    expect(outcome()).toEqual({ error: new CommandError('disconnected', 'connection lost.') })
  })

  it('should settle a held command sent again with its id', async () => {
    const { tracker, delivered } = setup()
    const outcome = track(tracker.send(RUN_START))
    await vi.advanceTimersByTimeAsync(0)

    tracker.hold()
    tracker.resendHeld()
    await vi.advanceTimersByTimeAsync(0)
    tracker.settle({ ...SAVED, ref: idOf(delivered[0]) })
    await vi.advanceTimersByTimeAsync(0)

    expect([delivered.map(idOf), outcome()]).toEqual([[idOf(delivered[0]), idOf(delivered[0])], { saved: { ...SAVED, ref: idOf(delivered[0]) } }])
  })

  it('should not time out a held command while the session restarts', async () => {
    const { tracker } = setup()
    const outcome = track(tracker.send(RUN_START))
    await vi.advanceTimersByTimeAsync(0)

    tracker.hold()
    await vi.advanceTimersByTimeAsync(COMMAND_TIMEOUT_MS * 2)

    expect(outcome()).toBe('pending')
  })

  it('should not start the timer of a command held while its delivery runs', async () => {
    let finishDelivery: () => void = () => undefined
    const { tracker } = setup(() => new Promise<void>((resolve) => (finishDelivery = resolve)))
    const outcome = track(tracker.send(RUN_START))

    tracker.hold()
    finishDelivery()
    await vi.advanceTimersByTimeAsync(COMMAND_TIMEOUT_MS * 2)

    expect(outcome()).toBe('pending')
  })

  it('should fail a command that loses its session a second time', async () => {
    const { tracker } = setup()
    const outcome = track(tracker.send(RUN_START))
    await vi.advanceTimersByTimeAsync(0)

    tracker.hold()
    tracker.resendHeld()
    await vi.advanceTimersByTimeAsync(0)
    tracker.hold()
    await vi.advanceTimersByTimeAsync(0)

    expect(outcome()).toEqual({ error: new CommandError('disconnected', 'connection lost.') })
  })

  it('should time out a resent command 15 seconds after it goes out again', async () => {
    const { tracker } = setup()
    const outcome = track(tracker.send(RUN_START))
    await vi.advanceTimersByTimeAsync(0)

    tracker.hold()
    tracker.resendHeld()
    await vi.advanceTimersByTimeAsync(COMMAND_TIMEOUT_MS)

    expect(outcome()).toEqual({ error: new CommandError('timeout', 'no answer from your machine.') })
  })

  it('should send again only the commands held for the lost session', async () => {
    const { tracker, delivered } = setup()
    track(tracker.send(RUN_START))
    await vi.advanceTimersByTimeAsync(0)

    tracker.resendHeld()
    await vi.advanceTimersByTimeAsync(0)

    expect(delivered).toHaveLength(1)
  })

  it('should reject every waiting command as disconnected when the connection drops', async () => {
    const { tracker } = setup()
    const outcomes = [track(tracker.send(RUN_START)), track(tracker.send(AD_MARK))]
    await vi.advanceTimersByTimeAsync(0)

    tracker.failAll()
    await vi.advanceTimersByTimeAsync(0)

    expect(outcomes.map((outcome) => outcome())).toEqual([
      { error: new CommandError('disconnected', 'connection lost.') },
      { error: new CommandError('disconnected', 'connection lost.') },
    ])
  })
})
