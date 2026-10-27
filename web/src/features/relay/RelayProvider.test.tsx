import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useEffect } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { QUERY_KEYS } from '@/app/queryKeys'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Payload } from '@/features/relay/contract/payload.gen'
import { RelayUnreachableError, TicketRejectedError } from '@/features/relay/handleClient'
import { type Relay, RelayDepsContext, type RelayDeps, useConnection, useRelay } from '@/features/relay/relayContext'
import { RelayProvider } from '@/features/relay/RelayProvider'
import { useCommand } from '@/features/relay/useCommand'
import { CliPeer, FakeRelay, type PeerOptions } from '@/test/fakeRelay'
import { flush, until } from '@/test/flush'
import { answerUnknownDevice, failingFirstHandle, LAST_SEEN_KEY, relayDeps, storedPairing } from '@/test/relayFixtures'

const ADS = payloadFixtures['ads.snapshot']
const STATUS = payloadFixtures.status
const RUN_START = { type: 'run.start', v: 1 } as const

let unmount: (() => void) | undefined
let captured: Relay | undefined

afterEach(() => {
  unmount?.()
  unmount = undefined
  captured = undefined
  Reflect.deleteProperty(document, 'visibilityState')
  Reflect.deleteProperty(navigator, 'onLine')
})

function Probe() {
  const connection = useConnection()
  const relay = useRelay()
  useEffect(() => {
    captured = relay
  }, [relay])
  const command = useCommand<typeof RUN_START>()
  const failure = command.error
  return (
    <>
      <p>state {connection.kind}</p>
      <p>{command.disabled ? 'edits off' : 'edits on'}</p>
      <p>{command.isSuccess ? 'saved' : failure ? `failed ${failure.code}` : 'idle'}</p>
      <button type="button" onClick={() => command.mutate(RUN_START)}>
        Run now
      </button>
      <button type="button" onClick={() => void relay.unpair()}>
        Unpair
      </button>
      <button type="button" onClick={() => relay.retryNow()}>
        Try now
      </button>
    </>
  )
}

async function renderRelay(overrides: Partial<RelayDeps> = {}, peerOptions: PeerOptions = {}) {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay, peerOptions)
  const queryClient = createQueryClient()
  const cleared: number[] = []
  const pairing = await storedPairing({ cliStaticPublicKey: peer.cli.staticPublic })
  const deps = relayDeps(relay, { loadPairing: async () => pairing, clearPairingOf: async () => void cleared.push(1), ...overrides })
  const view = render(
    <QueryClientProvider client={queryClient}>
      <RelayDepsContext value={deps}>
        <RelayProvider>
          <Probe />
        </RelayProvider>
      </RelayDepsContext>
    </QueryClientProvider>,
  )
  unmount = view.unmount
  return { relay, peer, queryClient, cleared, user: userEvent.setup(), view }
}

type Context = Awaited<ReturnType<typeof renderRelay>>

async function renderReady() {
  const context = await renderRelay()
  await until(() => context.relay.events !== undefined)
  context.relay.presence(true)
  await screen.findByText('state ready')
  return context
}

async function sendRunNow({ peer, user }: Context): Promise<void> {
  await user.click(screen.getByRole('button', { name: 'Run now' }))
  await until(() => peer.received.length > 0)
}

function lastReceivedId(peer: CliPeer): string {
  const command = JSON.parse(peer.received.at(-1) ?? '{}') as { id?: string }
  return command.id ?? ''
}

describe('RelayProvider', () => {
  it('should allow edits once ready', async () => {
    await renderReady()

    expect(screen.getByText('edits on')).toBeInTheDocument()
  })

  it('should keep edits off before the session is ready', async () => {
    await renderRelay()

    expect(await screen.findByText('state connecting')).toBeInTheDocument()
    expect(screen.getByText('edits off')).toBeInTheDocument()
  })

  it('should write a pushed snapshot into the query cache', async () => {
    const { peer, queryClient } = await renderReady()

    await peer.sendData(JSON.stringify(ADS))
    await until(() => queryClient.getQueryData(QUERY_KEYS.ads) !== undefined)

    expect(queryClient.getQueryData(QUERY_KEYS.ads)).toEqual(ADS)
  })

  it('should finish a command when the machine replies saved', async () => {
    const context = await renderReady()

    await sendRunNow(context)
    await context.peer.sendData(JSON.stringify({ ...payloadFixtures.saved, ref: lastReceivedId(context.peer) }))

    expect(await screen.findByText('saved')).toBeInTheDocument()
  })

  it('should fail a waiting command when the machine goes offline', async () => {
    const context = await renderReady()
    await sendRunNow(context)

    context.relay.presence(false)

    expect(await screen.findByText('failed disconnected')).toBeInTheDocument()
  })

  it('should send a command again over a new session when the restarted machine no longer knows the old one', async () => {
    const context = await renderReady()
    const { peer } = context
    await sendRunNow(context)
    const firstId = lastReceivedId(peer)
    const firstSid = peer.sid

    peer.sendHello({ t: 'unknown_session' })
    await until(() => peer.sid !== firstSid && peer.received.length > 1)
    await peer.sendData(JSON.stringify({ ...payloadFixtures.saved, ref: lastReceivedId(peer) }))

    expect(await screen.findByText('saved')).toBeInTheDocument()
    expect(lastReceivedId(peer)).toBe(firstId)
  })

  it('should fail the command when the machine loses the new session too', async () => {
    const context = await renderReady()
    const { peer } = context
    await sendRunNow(context)
    const firstSid = peer.sid
    peer.sendHello({ t: 'unknown_session' })
    await until(() => peer.sid !== firstSid && peer.received.length > 1)

    peer.sendHello({ t: 'unknown_session' })

    expect(await screen.findByText('failed disconnected')).toBeInTheDocument()
  })

  it('should fail a held command when the new session fails', async () => {
    const context = await renderReady()
    const { peer, relay } = context
    await sendRunNow(context)
    const sent = relay.sent.length
    peer.options.replyToCommit = false

    peer.sendHello({ t: 'unknown_session' })
    await until(() => relay.sent.slice(sent).some((envelope) => envelope.kind === 'hello' && envelope.sid !== peer.sid))
    peer.sendHello({ t: 'unknown_device' }, relay.sent.at(-1)?.sid)

    expect(await screen.findByText('failed disconnected')).toBeInTheDocument()
  })

  it('should clear the cached data when the machine unpairs this browser', async () => {
    const { peer, queryClient } = await renderReady()
    queryClient.setQueryData(QUERY_KEYS.ads, ADS)

    await peer.sendData(JSON.stringify(payloadFixtures.unpaired as Payload))
    await screen.findByText('state unpaired')

    expect(queryClient.getQueryData(QUERY_KEYS.ads)).toBeUndefined()
  })

  it('should forget the pairing without replying when the machine unpairs this browser', async () => {
    const { peer, cleared } = await renderReady()

    await peer.sendData(JSON.stringify(payloadFixtures.unpaired as Payload))
    await screen.findByText('state unpaired')

    expect([cleared, peer.received]).toEqual([[1], []])
  })

  it('should keep the cached data while the session restarts', async () => {
    const { peer, queryClient } = await renderReady()
    queryClient.setQueryData(QUERY_KEYS.ads, ADS)

    peer.sendHello({ t: 'abort' })
    await screen.findByText('state handshaking')

    expect(queryClient.getQueryData(QUERY_KEYS.ads)).toEqual(ADS)
  })

  it('should clear the cached data when the relay rejects the ticket', async () => {
    const queryClient = createQueryClient()
    queryClient.setQueryData(QUERY_KEYS.status, STATUS)
    const relay = new FakeRelay()
    const pairing = await storedPairing()
    render(
      <QueryClientProvider client={queryClient}>
        <RelayDepsContext
          value={relayDeps(relay, {
            loadPairing: async () => pairing,
            clearPairingOf: async () => undefined,
            fetchHandle: async () => Promise.reject(new TicketRejectedError()),
          })}
        >
          <RelayProvider>
            <Probe />
          </RelayProvider>
        </RelayDepsContext>
      </QueryClientProvider>,
    )

    await screen.findByText('state unpaired')

    expect(queryClient.getQueryData(QUERY_KEYS.status)).toBeUndefined()
  })

  it('should unpair on the machine and in the browser', async () => {
    const { peer, queryClient, cleared, user } = await renderReady()
    queryClient.setQueryData(QUERY_KEYS.ads, ADS)

    await user.click(screen.getByRole('button', { name: 'Unpair' }))
    await screen.findByText('state unpaired')

    expect([peer.received.map((text) => (JSON.parse(text) as Payload).type), queryClient.getQueryData(QUERY_KEYS.ads), cleared]).toEqual([
      ['unpair'],
      undefined,
      [1],
    ])
  })

  it('should forget the pairing when the user pairs again after a failure', async () => {
    const { relay, peer, cleared, user } = await renderRelay({}, { replyToCommit: false })
    await answerUnknownDevice(relay, peer)
    await screen.findByText('state failed')

    await user.click(screen.getByRole('button', { name: 'Unpair' }))

    expect([await screen.findByText('state unpaired'), cleared]).toEqual([expect.anything(), [1]])
  })

  it('should stop the connection on unmount', async () => {
    const { relay, view } = await renderReady()

    view.unmount()
    unmount = undefined

    await until(() => relay.stopped)
    expect(relay.stopped).toBe(true)
  })

  it('should retry at once when the page becomes visible', async () => {
    const { relay } = await renderRelay({ fetchHandle: failingFirstHandle() })
    await screen.findByText('state relayDown')

    document.dispatchEvent(new Event('visibilitychange'))
    await until(() => relay.handles.length > 0)

    expect(relay.handles).toEqual(['handle-two'])
  })

  it('should remember when the machine was last seen', async () => {
    const { peer, queryClient } = await renderReady()

    await peer.sendData(JSON.stringify(ADS))
    await until(() => queryClient.getQueryData(QUERY_KEYS.ads) !== undefined)

    expect(Number.isNaN(Date.parse(localStorage.getItem(LAST_SEEN_KEY) ?? ''))).toBe(false)
  })

  it('should fail a waiting command when the provider goes away', async () => {
    const { peer, view } = await renderReady()
    const reply = captured?.send(RUN_START)
    await until(() => peer.received.length > 0)

    view.unmount()
    unmount = undefined

    await expect(reply).rejects.toMatchObject({ code: 'disconnected' })
  })

  it('should reject a command sent after the provider goes away as disconnected', async () => {
    const { view } = await renderReady()
    const relay = captured

    view.unmount()
    unmount = undefined

    await expect(relay?.send(RUN_START)).rejects.toMatchObject({ code: 'disconnected' })
  })

  it('should retry at once on Try now', async () => {
    const { relay, user } = await renderRelay({ fetchHandle: failingFirstHandle() })
    await screen.findByText('state relayDown')

    await user.click(screen.getByRole('button', { name: 'Try now' }))
    await until(() => relay.handles.length > 0)

    expect(relay.handles).toEqual(['handle-two'])
  })

  it('should not retry when the page becomes hidden', async () => {
    let calls = 0
    await renderRelay({
      fetchHandle: async () => {
        calls += 1
        throw new RelayUnreachableError('offline')
      },
    })
    await screen.findByText('state relayDown')
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' })

    document.dispatchEvent(new Event('visibilitychange'))
    await flush()

    expect(calls).toBe(1)
  })

  it('should turn edits off as soon as the browser goes offline', async () => {
    await renderReady()

    window.dispatchEvent(new Event('offline'))
    await screen.findByText('state networkOffline')

    expect(screen.getByText('edits off')).toBeInTheDocument()
  })

  it('should reconnect when the browser is back online', async () => {
    const { relay } = await renderReady()
    window.dispatchEvent(new Event('offline'))
    await screen.findByText('state networkOffline')

    window.dispatchEvent(new Event('online'))
    await until(() => relay.handles.length > 1)

    expect(relay.handles).toHaveLength(2)
  })

  it('should wait for the network when the page opens offline', async () => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false })

    const { relay } = await renderRelay()

    expect([await screen.findByText('state networkOffline'), relay.handles]).toEqual([expect.anything(), []])
  })

  it('should name the missing provider when used outside it', () => {
    function Lonely() {
      useConnection()
      return null
    }

    expect(() => render(<Lonely />)).toThrow('useRelay needs a RelayProvider above it')
  })
})
