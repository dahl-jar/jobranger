import 'fake-indexeddb/auto'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ClaimResult } from '@/features/pairing/claimPairing'
import { PairPage } from '@/features/pairing/PairPage'
import { PairingDepsContext } from '@/features/pairing/pairingDeps'
import { PAIR_CONFIRM_TIMEOUT_SECONDS, STEP_TIMEOUT_SECONDS } from '@/features/relay/contract/limits'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { clearPairing, loadPairing, savePairing } from '@/features/relay/keyStore'
import type { ConnectRelay } from '@/features/relay/transport'
import { createQueryClient } from '@/app/queryClient'
import { RelayDepsContext } from '@/features/relay/relayContext'
import { CLAIM_OK, LAST_SEEN_KEY, relayDeps, SEEN_AT, storedPairing, TEST_AGENT, withoutCliKey } from '@/test/relayFixtures'
import { CliPeer, FakeRelay, type PeerOptions, TEST_CHANNEL } from '@/test/fakeRelay'
import { flush, until } from '@/test/flush'
import { cliPairTag } from '@/test/fakeCli'
import { decodeBase64Url } from '@/features/relay/base64url'

const STATUS = JSON.stringify(payloadFixtures.status)

async function renderPair(claimResult: ClaimResult | Promise<ClaimResult> = CLAIM_OK, peerOptions: PeerOptions = {}, connect?: ConnectRelay) {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay, peerOptions)
  const user = userEvent.setup()
  const view = render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={['/pair']}>
        <PairingDepsContext value={{ claim: async () => claimResult, connect: connect ?? relay.connect, agent: TEST_AGENT }}>
          <RelayDepsContext value={relayDeps(relay)}>
            <PairPage />
          </RelayDepsContext>
        </PairingDepsContext>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { relay, peer, user, view }
}

const MACHINE_SILENT = "Your machine didn't answer. Run jobranger pair again."
const MS_PER_SECOND = 1000

async function submitWithoutTimers() {
  await until(() => screen.queryByLabelText('Enter the code it shows') !== null)
  fireEvent.change(screen.getByLabelText('Enter the code it shows'), { target: { value: 'kqfb-mxtw' } })
  fireEvent.click(screen.getByRole('button', { name: 'Connect' }))
}

async function submitCode(user: ReturnType<typeof userEvent.setup>, code = 'kqfb-mxtw') {
  await user.type(await screen.findByLabelText('Enter the code it shows'), code)
  await user.click(screen.getByRole('button', { name: 'Connect' }))
}

async function startPairing(peerOptions: PeerOptions = {}) {
  const context = await renderPair(CLAIM_OK, peerOptions)
  await submitCode(context.user)
  await waitFor(() => expect(context.relay.events).toBeDefined())
  context.relay.presence(true)
  return context
}

async function showWords(claimResult?: ClaimResult) {
  const context = await renderPair(claimResult)
  await submitCode(context.user)
  await waitFor(() => expect(context.relay.events).toBeDefined())
  context.relay.presence(true)
  await screen.findByRole('heading', { name: 'Do these words match your terminal?' })
  return context
}

async function answerYes() {
  const context = await showWords()
  await context.user.click(screen.getByRole('button', { name: 'Yes, they match' }))
  await until(() => context.peer.browserConfirmed)
  return context
}

afterEach(async () => {
  vi.useRealTimers()
  await clearPairing()
})

describe('PairPage', () => {
  it('should show the four CLI words after the code is accepted', async () => {
    const { peer } = await showWords()

    expect(screen.getByRole('list', { name: 'Confirmation words' })).toHaveTextContent(peer.cli.words.join(''))
  })

  it('should show the paired facts after the first CLI status', async () => {
    const { peer } = await answerYes()

    await peer.sendData(STATUS)

    expect(await screen.findByRole('heading', { name: 'Your computer is paired' })).toBeInTheDocument()
    expect(screen.getByText('laptop')).toBeInTheDocument()
  })

  it('should store the CLI key once the first CLI frame verifies', async () => {
    const { peer } = await answerYes()

    await peer.sendData(STATUS)
    await screen.findByRole('heading', { name: 'Your computer is paired' })

    await waitFor(async () => expect((await loadPairing())?.cliStaticPublicKey).toEqual(peer.cli.staticPublic))
    expect((await loadPairing())?.pending).toBe(false)
  })

  it('should say pairing was refused when the CLI aborts while confirming', async () => {
    const { peer } = await showWords()

    peer.sendHello({ t: 'abort' })

    expect(await screen.findByText('Pairing refused in the terminal.')).toBeInTheDocument()
  })

  it('should forget the pairing when the CLI aborts while confirming', async () => {
    const { peer } = await showWords()

    peer.sendHello({ t: 'abort' })

    await waitFor(async () => expect(await loadPairing()).toBeUndefined())
  })

  it('should clear the pending pairing when the connection drops before the first CLI data frame', async () => {
    const { relay } = await answerYes()

    relay.drop()

    expect(await screen.findByText('The connection dropped. Run jobranger pair again.')).toBeInTheDocument()
    expect(await loadPairing()).toBeUndefined()
  })

  it('should say the machine unpaired this browser', async () => {
    const { peer } = await answerYes()
    await peer.sendData(STATUS)
    await screen.findByRole('heading', { name: 'Your computer is paired' })

    await peer.sendData(JSON.stringify(payloadFixtures.unpaired))

    expect(await screen.findByText('Your machine unpaired this browser.')).toBeInTheDocument()
  })

  it('should forget the pairing when the machine unpairs this browser', async () => {
    const { peer } = await answerYes()
    await peer.sendData(STATUS)
    await screen.findByRole('heading', { name: 'Your computer is paired' })

    await peer.sendData(JSON.stringify(payloadFixtures.unpaired))

    await waitFor(async () => expect(await loadPairing()).toBeUndefined())
  })

  it('should clear the last seen time when the pairing ends', async () => {
    localStorage.setItem(LAST_SEEN_KEY, SEEN_AT)
    const { peer } = await answerYes()
    await peer.sendData(STATUS)
    await screen.findByRole('heading', { name: 'Your computer is paired' })

    await peer.sendData(JSON.stringify(payloadFixtures.unpaired))
    await screen.findByText('Your machine unpaired this browser.')

    expect(localStorage.getItem(LAST_SEEN_KEY)).toBeNull()
  })

  it('should tell the machine when Pair a different machine is pressed on a live pairing', async () => {
    const { peer, user } = await answerYes()
    await peer.sendData(STATUS)

    await user.click(await screen.findByRole('button', { name: 'Pair a different machine' }))

    await until(() => peer.received.length > 0)
    expect(JSON.parse(peer.received[0] ?? '{}')).toMatchObject({ type: 'unpair', v: 1 })
  })

  it('should say pairing was cancelled when the website says No', async () => {
    const { user } = await showWords()

    await user.click(screen.getByRole('button', { name: 'No' }))

    expect(await screen.findByText('Pairing cancelled. Run jobranger pair again.')).toBeInTheDocument()
  })

  it('should forget the pairing when the website says No', async () => {
    const { user } = await showWords()

    await user.click(screen.getByRole('button', { name: 'No' }))

    await waitFor(async () => expect(await loadPairing()).toBeUndefined())
  })

  it('should keep the pending pairing in storage while the words show', async () => {
    await showWords()

    expect((await loadPairing())?.pending).toBe(true)
  })

  it('should use a new web key for every pairing', async () => {
    const { relay, user } = await showWords()
    await user.click(screen.getByRole('button', { name: 'No' }))
    await screen.findByText('Pairing cancelled. Run jobranger pair again.')

    await submitCode(user)
    await waitFor(() => expect(relay.handles).toHaveLength(2))
    relay.presence(true)
    await until(() => relay.steps().filter((step) => step === 'commit').length === 2)

    const keys = relay.peer?.commits.map((step) => step.browser_static)
    expect(new Set(keys).size).toBe(2)
  })

  it('should move focus to the words heading', async () => {
    await showWords()

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Do these words match your terminal?' })).toHaveFocus())
  })

  it('should show only the word compare while the words show', async () => {
    await showWords()

    expect(screen.getAllByRole('heading', { level: 1 }).map((heading) => heading.textContent)).toEqual(['Do these words match your terminal?'])
  })

  it('should describe the code input with the error', async () => {
    const { user } = await renderPair({ kind: 'wrong' })

    await submitCode(user)

    await screen.findByRole('alert')
    expect(screen.getByLabelText('Enter the code it shows')).toHaveAccessibleDescription(
      "That code doesn't match. Check your terminal. 8 letters, valid for 10 minutes.",
    )
  })

  it('should describe the code input with the help text only before an error', async () => {
    await renderPair()

    expect(await screen.findByLabelText('Enter the code it shows')).toHaveAccessibleDescription('8 letters, valid for 10 minutes.')
  })

  it('should send the tag of the claimed code in the commit', async () => {
    const { relay } = await showWords()

    expect(relay.peer?.commits.map((step) => step.pair_tag)).toEqual([await cliPairTag('KQFB-MXTW', decodeBase64Url(TEST_CHANNEL))])
  })

  it('should say it is connecting once the code is accepted', async () => {
    const { user } = await renderPair()

    await submitCode(user)

    expect(await screen.findByRole('heading', { name: 'Connecting to your machine' })).toBeInTheDocument()
  })

  it('should wait for the terminal after Yes', async () => {
    await answerYes()

    expect(screen.getByRole('heading', { name: 'Waiting for your terminal' })).toBeInTheDocument()
  })

  it('should move focus to the waiting heading after Yes', async () => {
    await answerYes()

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Waiting for your terminal' })).toHaveFocus())
  })

  it('should move focus to the paired heading after the first CLI status', async () => {
    const { peer } = await answerYes()

    await peer.sendData(STATUS)

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Your computer is paired' })).toHaveFocus())
  })

  it('should leave focus alone when the page opens on a stored pairing', async () => {
    await savePairing(await storedPairing())

    await renderPair()

    await screen.findByRole('heading', { name: 'Your computer is paired' })
    expect(document.body).toHaveFocus()
  })

  it('should say the machine stopped answering when the CLI goes quiet after the commit', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { relay } = await renderPair(CLAIM_OK, { replyToCommit: false })
    await submitWithoutTimers()
    await until(() => relay.events !== undefined)
    relay.presence(true)
    await until(() => relay.steps().length === 1)

    vi.advanceTimersByTime(STEP_TIMEOUT_SECONDS * MS_PER_SECOND)

    await until(() => screen.queryByText(MACHINE_SILENT) !== null)
    vi.useRealTimers()
    expect(screen.getByText(MACHINE_SILENT)).toBeInTheDocument()
  })

  it('should stay silent when the CLI comes back after the claim expired', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { relay } = await renderPair()
    await submitWithoutTimers()
    await until(() => relay.events !== undefined)

    vi.advanceTimersByTime(PAIR_CONFIRM_TIMEOUT_SECONDS * MS_PER_SECOND)
    await until(() => screen.queryByText(MACHINE_SILENT) !== null)
    await until(() => relay.stopped)
    relay.presence(true)
    await flush()

    vi.useRealTimers()
    expect([relay.sent, await loadPairing()]).toEqual([[], undefined])
  })

  it('should say pairing failed when the CLI MAC does not verify', async () => {
    await startPairing({ confirmMac: (mac) => mac.map((byte) => byte ^ 1) })

    expect(await screen.findByText('Pairing failed. Run jobranger pair again.')).toBeInTheDocument()
    expect(await loadPairing()).toBeUndefined()
  })

  it('should clear the pairing when the relay cannot be reached after the claim', async () => {
    const { user } = await renderPair(CLAIM_OK, {}, async () => Promise.reject(new Error('hub unreachable')))

    await submitCode(user)

    expect(await screen.findByText('The connection dropped. Run jobranger pair again.')).toBeInTheDocument()
    await waitFor(async () => expect(await loadPairing()).toBeUndefined())
  })

  it('should keep the paired facts when the CLI sends other messages', async () => {
    const { peer } = await answerYes()
    await peer.sendData(STATUS)
    await screen.findByText('laptop')

    await peer.sendData(JSON.stringify(payloadFixtures.saved))
    await flush()

    expect(screen.getByText('laptop')).toBeInTheDocument()
  })

  it('should keep a completed pairing when the page closes', async () => {
    const { peer, view } = await answerYes()
    await peer.sendData(STATUS)
    await screen.findByRole('heading', { name: 'Your computer is paired' })
    await waitFor(async () => expect((await loadPairing())?.pending).toBe(false))

    view.unmount()
    await flush()

    expect((await loadPairing())?.pending).toBe(false)
  })

  it('should keep a complete stored pairing on load', async () => {
    await savePairing(await storedPairing())

    await renderPair()

    await screen.findByRole('heading', { name: 'Your computer is paired' })
    expect((await loadPairing())?.channel).toBe(TEST_CHANNEL)
  })

  it('should clear a stored pairing that has no CLI key', async () => {
    await savePairing(withoutCliKey(await storedPairing()))

    await renderPair()

    expect(await screen.findByLabelText('Enter the code it shows')).toBeInTheDocument()
    expect(await loadPairing()).toBeUndefined()
  })

  it('should show the expired message when the claim returns 410', async () => {
    const { user } = await renderPair({ kind: 'expired' })

    await submitCode(user)

    expect(await screen.findByRole('alert')).toHaveTextContent('This code has expired. Run jobranger pair again.')
  })

  it('should keep the typed code when the claim returns 404', async () => {
    const { user } = await renderPair({ kind: 'wrong' })

    await submitCode(user)

    await screen.findByRole('alert')
    expect(screen.getByLabelText('Enter the code it shows')).toHaveValue('kqfb-mxtw')
  })

  it('should focus the input when the claim returns 404', async () => {
    const { user } = await renderPair({ kind: 'wrong' })

    await submitCode(user)

    await screen.findByRole('alert')
    expect(screen.getByLabelText('Enter the code it shows')).toHaveFocus()
  })

  it.each([
    { result: { kind: 'wrong' }, message: "That code doesn't match. Check your terminal." },
    { result: { kind: 'rateLimited' }, message: 'Too many tries. Wait a minute and try again.' },
    { result: { kind: 'unreachable' }, message: "Can't reach the relay. Try again." },
    { result: { kind: 'invalid' }, message: 'Check the code. It has 8 letters.' },
  ] as const)('should say "$message" when the claim returns $result.kind', async ({ result, message }) => {
    const { user } = await renderPair(result)

    await submitCode(user)

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })

  it.each([
    { code: ' ', message: 'Enter the code from your terminal.' },
    { code: 'kqfa-mxtw', message: 'Check the code. It has 8 letters.' },
  ])('should say "$message" for "$code"', async ({ code, message }) => {
    const { user } = await renderPair()

    await submitCode(user, code)

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })

  it('should mark the input invalid for a rejected code', async () => {
    const { user } = await renderPair()

    await submitCode(user, 'kqfa-mxtw')

    await screen.findByRole('alert')
    expect(screen.getByLabelText('Enter the code it shows')).toHaveAttribute('aria-invalid', 'true')
  })

  it('should disable Connect while the claim runs', async () => {
    const { user } = await renderPair(new Promise<ClaimResult>(() => undefined))

    await submitCode(user)

    expect(await screen.findByRole('button', { name: 'Connecting...' })).toBeDisabled()
  })

  it('should forget a pending pairing left by a reload', async () => {
    await savePairing(withoutCliKey(await storedPairing({ pending: true })))

    await renderPair()

    await screen.findByLabelText('Enter the code it shows')
    expect(await loadPairing()).toBeUndefined()
  })

  it('should clear a pending pairing when the page closes', async () => {
    const { view } = await showWords()

    view.unmount()

    await waitFor(async () => expect(await loadPairing()).toBeUndefined())
  })

  it('should show a clean form after Pair a different machine', async () => {
    await savePairing(await storedPairing())
    const { user } = await renderPair()

    await user.click(await screen.findByRole('button', { name: 'Pair a different machine' }))

    expect(await screen.findByLabelText('Enter the code it shows')).toBeInTheDocument()
    expect([await loadPairing(), screen.queryByRole('alert')]).toEqual([undefined, null])
  })

  it('should link Open jobranger to the app', async () => {
    await savePairing(await storedPairing())

    await renderPair()

    expect(await screen.findByRole('link', { name: 'Open jobranger' })).toHaveAttribute('href', '/app/today')
  })
})
