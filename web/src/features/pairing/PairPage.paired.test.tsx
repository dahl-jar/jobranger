import 'fake-indexeddb/auto'
import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { PairPage } from '@/features/pairing/PairPage'
import { PairingDepsContext } from '@/features/pairing/pairingDeps'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Payload } from '@/features/relay/contract/payload.gen'
import { clearPairing, loadPairing, savePairing } from '@/features/relay/keyStore'
import { RelayUnreachableError } from '@/features/relay/handleClient'
import { RelayDepsContext } from '@/features/relay/relayContext'
import { CliPeer, FakeRelay, type PeerOptions, TEST_CHANNEL } from '@/test/fakeRelay'
import { flush, until } from '@/test/flush'
import { answerUnknownDevice, relayDeps, storedPairing, TEST_AGENT } from '@/test/relayFixtures'

const QUICK_MS = 500
const OTHER_CHANNEL = 'b3RoZXItY2hhbm5lbC1vdGhlci1jaGFubmVsLW90aGVy'

interface Options {
  stored?: boolean
  reason?: string
  fetchHandle?: () => Promise<string>
  peer?: PeerOptions
}

async function renderPaired({ stored = true, reason, fetchHandle = async () => 'handle-one', peer: peerOptions = {} }: Options = {}) {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay, peerOptions)
  if (stored) await savePairing(await storedPairing({ cliStaticPublicKey: peer.cli.staticPublic }))
  const view = render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={[{ pathname: '/pair', state: reason === undefined ? null : { reason } }]}>
        <PairingDepsContext value={{ claim: async () => ({ kind: 'unreachable' }), connect: relay.connect, agent: TEST_AGENT }}>
          <RelayDepsContext value={relayDeps(relay, { fetchHandle })}>
            <PairPage />
          </RelayDepsContext>
        </PairingDepsContext>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { relay, peer, view, user: userEvent.setup() }
}

async function renderLive() {
  const context = await renderPaired()
  await until(() => context.relay.events !== undefined)
  context.relay.presence(true)
  await screen.findByText('Connected')
  return context
}

afterEach(async () => {
  await clearPairing()
})

describe('PairPage with a stored pairing', () => {
  it('should say it is connecting until the session with the machine is ready', async () => {
    await renderPaired()

    expect(await screen.findByText('Connecting to your machine...')).toBeInTheDocument()
    expect(screen.queryByText('Connected')).not.toBeInTheDocument()
  })

  it('should show the live machine facts once connected', async () => {
    const { peer } = await renderLive()

    await peer.sendData(JSON.stringify(payloadFixtures.status))

    expect((await screen.findByText('Machine')).nextElementSibling).toHaveTextContent('laptop')
  })

  it('should say the machine is offline when presence drops', async () => {
    const { relay } = await renderPaired()
    await until(() => relay.events !== undefined)

    relay.presence(false)

    expect(await screen.findByText('Your machine is offline')).toBeInTheDocument()
  })

  it('should say the relay cannot be reached', async () => {
    await renderPaired({ fetchHandle: async () => Promise.reject(new RelayUnreachableError('offline')) })

    expect(await screen.findByText("Can't reach the relay")).toBeInTheDocument()
  })

  it('should say the browser is offline', async () => {
    const { relay } = await renderPaired()
    await until(() => relay.events !== undefined)

    window.dispatchEvent(new Event('offline'))

    expect(await screen.findByText("You're offline")).toBeInTheDocument()
  })

  it('should say the machine does not know this browser', async () => {
    const { relay, peer } = await renderPaired({ peer: { replyToCommit: false } })

    await answerUnknownDevice(relay, peer)

    expect(await screen.findByText("Your machine doesn't know this browser")).toBeInTheDocument()
  })

  it('should tell the machine on Pair a different machine', async () => {
    const { peer, user } = await renderLive()

    await user.click(screen.getByRole('button', { name: 'Pair a different machine' }))
    await screen.findByLabelText('Enter the code it shows')

    expect(peer.received.map((text) => (JSON.parse(text) as Payload).type)).toEqual(['unpair'])
  })

  it('should say the machine unpaired this browser', async () => {
    const { peer } = await renderLive()

    await peer.sendData(JSON.stringify(payloadFixtures.unpaired))

    expect(await screen.findByText('Your machine unpaired this browser.')).toBeInTheDocument()
    expect(await loadPairing()).toBeUndefined()
  })
})

describe('PairPage while the relay holds the handle request', () => {
  async function clickPairDifferentMachine() {
    let release: (handle: string) => void = () => undefined
    const context = await renderPaired({ fetchHandle: () => new Promise((resolve) => (release = resolve)) })
    await screen.findByText('Connecting to your machine...')
    await context.user.click(screen.getByRole('button', { name: 'Pair a different machine' }))
    return { ...context, release: () => release('handle-late') }
  }

  it('should show the form at once on Pair a different machine', async () => {
    await clickPairDifferentMachine()

    expect(await screen.findByLabelText('Enter the code it shows', {}, { timeout: QUICK_MS })).toBeInTheDocument()
  })

  it('should clear the old pairing once the request settles', async () => {
    const { release } = await clickPairDifferentMachine()
    await screen.findByLabelText('Enter the code it shows', {}, { timeout: QUICK_MS })
    const beforeRelease = (await loadPairing())?.channel

    release()

    await vi.waitFor(async () => expect([beforeRelease, await loadPairing()]).toEqual([TEST_CHANNEL, undefined]))
  })

  it('should keep a new pairing saved before the late clear', async () => {
    const { release } = await clickPairDifferentMachine()
    await screen.findByLabelText('Enter the code it shows', {}, { timeout: QUICK_MS })
    await savePairing(await storedPairing({ channel: OTHER_CHANNEL }))

    release()
    await flush()

    expect((await loadPairing())?.channel).toBe(OTHER_CHANNEL)
  })
})

describe('PairPage after the app sent this browser here', () => {
  it.each([
    ['ticket', 'Pair this browser again.'],
    ['revoked', 'Your machine unpaired this browser.'],
    ['refused', 'Pairing refused in the terminal.'],
  ])('should explain the %s reason above the form', async (reason, text) => {
    await renderPaired({ stored: false, reason })

    expect(await screen.findByText(text)).toBeInTheDocument()
  })

  it('should show the plain form for an unknown reason', async () => {
    await renderPaired({ stored: false, reason: 'later' })

    await screen.findByLabelText('Enter the code it shows')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
