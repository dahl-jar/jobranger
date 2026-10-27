import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Envelope } from '@/features/relay/contract/envelope.gen'
import { connectHub } from '@/features/relay/hubTransport'
import type { RelayEvents, RelayTransport } from '@/features/relay/transport'
import { FakeHub } from '@/test/fakeHub'
import { TEST_CHANNEL } from '@/test/fakeRelay'

const ENVELOPE: Envelope = { v: 1, channel: TEST_CHANNEL, sid: 'Ee8hzATM8LYuaoJGRMtXQQ', seq: 0, kind: 'hello', ciphertext: 'eyJ0IjoiYWJvcnQifQ' }

let hub: FakeHub
let transport: RelayTransport | undefined
let events: { onEnvelope: ReturnType<typeof vi.fn>; onPresence: ReturnType<typeof vi.fn>; onClose: ReturnType<typeof vi.fn> }

async function connect(): Promise<RelayTransport> {
  transport = await connectHub(hub.url)('handle-one', events as unknown as RelayEvents)
  return transport
}

beforeEach(async () => {
  hub = await FakeHub.start()
  events = { onEnvelope: vi.fn(), onPresence: vi.fn(), onClose: vi.fn() }
})

afterEach(async () => {
  await transport?.stop()
  transport = undefined
  await hub.stop()
})

describe('connectHub', () => {
  it('should open /hub with the handle as the access token', async () => {
    await connect()

    expect([hub.requestUrls, hub.tokens]).toEqual([['/hub'], ['handle-one']])
  })

  it('should pass a Receive envelope on as JSON text', async () => {
    await connect()

    hub.call('Receive', ENVELOPE)

    await vi.waitFor(() => expect(events.onEnvelope).toHaveBeenCalledWith(JSON.stringify(ENVELOPE)))
  })

  it('should pass the CLI online flag of Presence on', async () => {
    await connect()

    hub.call('Presence', { cli_online: true, browsers: 2 })

    await vi.waitFor(() => expect(events.onPresence).toHaveBeenCalledWith({ cli_online: true }))
  })

  it.each([
    { name: 'a string online flag', presence: { cli_online: 'yes', browsers: 2 } },
    { name: 'a fractional browser count', presence: { cli_online: true, browsers: 1.5 } },
    { name: 'no browser count', presence: { cli_online: true } },
    { name: 'null', presence: null },
  ])('should ignore a Presence call with $name', async ({ presence }) => {
    await connect()

    hub.call('Presence', presence)
    hub.call('Presence', { cli_online: false, browsers: 0 })

    await vi.waitFor(() => expect(events.onPresence).toHaveBeenCalledOnce())
    expect(events.onPresence).toHaveBeenCalledWith({ cli_online: false })
  })

  it('should invoke Send with the envelope', async () => {
    const relay = await connect()

    await relay.send(ENVELOPE)

    expect(hub.invocations).toEqual([{ target: 'Send', arguments: [ENVELOPE] }])
  })

  it('should close the socket on stop', async () => {
    const relay = await connect()

    await relay.stop()

    await vi.waitFor(() => expect(hub.clientClosed).toBe(true))
  })

  it('should report a close from the relay', async () => {
    await connect()

    hub.closeClient()

    await vi.waitFor(() => expect(events.onClose).toHaveBeenCalledOnce())
  })
})
