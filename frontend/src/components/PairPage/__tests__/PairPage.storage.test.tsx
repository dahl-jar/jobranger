import 'fake-indexeddb/auto'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PairPage } from '@/components/PairPage/PairPage'
import { PairingDepsContext } from '@/components/PairPage/pairingDepsContext'
import { payloadFixtures } from '@/contract/fixtures.gen'
import { clearPairing, loadPairing, probeStorage, savePairing, StorageError } from '@/services/pairingStore/pairingStorage'
import { CliPeer, FakeRelay } from '@/__tests__/fakeRelay'
import { until } from '@/__tests__/waitForAsyncWork'
import { CLAIM_OK, TEST_AGENT } from '@/__tests__/relayFixtures'

vi.mock('@/services/pairingStore/pairingStorage', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/services/pairingStore/pairingStorage')>()
  return {
    ...actual,
    loadPairing: vi.fn(actual.loadPairing),
    savePairing: vi.fn(actual.savePairing),
    probeStorage: vi.fn(actual.probeStorage),
  }
})

const STORAGE_MESSAGE = "This browser can't save the pairing. Allow site data and try again."
const BLOCKED = new DOMException('storage blocked', 'InvalidStateError')

async function renderPage() {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay)
  const claim = vi.fn(async () => CLAIM_OK)
  const user = userEvent.setup()
  render(
    <MemoryRouter initialEntries={['/pair']}>
      <PairingDepsContext value={{ claim, connect: relay.connect, agent: TEST_AGENT }}>
        <PairPage />
      </PairingDepsContext>
    </MemoryRouter>,
  )
  await user.type(await screen.findByLabelText('Enter the code it shows'), 'KQFBMXTW')
  return { relay, peer, claim, user }
}

afterEach(async () => {
  await clearPairing()
})

describe('PairPage without storage', () => {
  it('should show the form when the stored pairing cannot be read', async () => {
    vi.mocked(loadPairing).mockRejectedValueOnce(BLOCKED)

    await renderPage()

    expect(screen.getByLabelText('Enter the code it shows')).toBeInTheDocument()
  })

  it('should say the browser cannot save the pairing without spending the code', async () => {
    vi.mocked(probeStorage).mockResolvedValueOnce(false)
    const { claim, user } = await renderPage()

    await user.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(STORAGE_MESSAGE)
    expect(claim).not.toHaveBeenCalled()
  })

  it('should say the browser cannot save the pairing when the first save fails', async () => {
    vi.mocked(savePairing).mockRejectedValueOnce(BLOCKED)
    const { user } = await renderPage()

    await user.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(STORAGE_MESSAGE)
  })

  it('should end the pairing with the storage error when the CLI key cannot be saved', async () => {
    const { relay, peer, user } = await renderPage()
    await user.click(screen.getByRole('button', { name: 'Connect' }))
    await waitFor(() => expect(relay.events).toBeDefined())
    relay.presence(true)
    await user.click(await screen.findByRole('button', { name: 'Yes, they match' }))
    await until(() => peer.browserConfirmed)
    vi.mocked(savePairing).mockRejectedValueOnce(new StorageError('saved pairing did not read back with its keys'))

    await peer.sendData(JSON.stringify(payloadFixtures.status))

    expect(await screen.findByRole('alert')).toHaveTextContent(STORAGE_MESSAGE)
    await waitFor(() => expect(screen.getByLabelText('Enter the code it shows')).toHaveFocus())
    await until(() => relay.stopped)
    expect([await loadPairing(), relay.steps().at(-1)]).toEqual([undefined, 'abort'])
  })
})
