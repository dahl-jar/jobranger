import 'fake-indexeddb/auto'
import { QueryClientProvider } from '@tanstack/react-query'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/components/App/createQueryClient'
import { PairCodeForm } from '@/components/PairPage/PairCodeForm'
import { PairConnected } from '@/components/PairPage/PairConnected'
import { PairPage } from '@/components/PairPage/PairPage'
import { PairingDepsContext } from '@/components/PairPage/pairingDepsContext'
import type { EndReason, FormError } from '@/components/PairPage/pairingReducer'
import { payloadFixtures } from '@/contract/fixtures.gen'
import type { Status } from '@/contract/payload.gen'
import { clearPairing } from '@/services/pairingStore/pairingStorage'
import { RelayDepsContext } from '@/hooks/useRelay'
import { expectNoEnglish } from '@/__tests__/findEnglishText'
import { CliPeer, FakeRelay } from '@/__tests__/fakeRelay'
import { until } from '@/__tests__/waitForAsyncWork'
import { CLAIM_OK, relayDeps, TEST_AGENT } from '@/__tests__/relayFixtures'
import { renderNorwegian } from '@/__tests__/renderWithProviders'

const STATUS = payloadFixtures.status as Status
const FORM_ERRORS: FormError[] = ['empty', 'invalid', 'wrong', 'expired', 'rateLimited', 'unreachable', 'storage']
const END_REASONS: EndReason[] = ['refused', 'timeout', 'silent', 'cancelled', 'dropped', 'failed', 'revoked', 'ticket']

async function startPairing() {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay)
  const user = userEvent.setup()
  renderNorwegian(
    <QueryClientProvider client={createQueryClient()}>
      <PairingDepsContext value={{ claim: async () => CLAIM_OK, connect: relay.connect, agent: TEST_AGENT }}>
        <RelayDepsContext value={relayDeps(relay)}>
          <PairPage />
        </RelayDepsContext>
      </PairingDepsContext>
    </QueryClientProvider>,
  )
  await user.type(await screen.findByLabelText('Skriv inn koden den viser'), 'kqfb-mxtw')
  await user.click(screen.getByRole('button', { name: 'Koble til' }))
  await waitFor(() => expect(relay.events).toBeDefined())
  return { relay, peer, user }
}

afterEach(async () => {
  vi.useRealTimers()
  await clearPairing()
})

describe('pairing in bokmål', () => {
  it('should show the code form without English text', () => {
    renderNorwegian(<PairCodeForm error={undefined} ended={undefined} busy={false} onSubmit={vi.fn()} />)

    expect(screen.getByRole('heading', { level: 1, name: 'Koble til maskinen din' })).toBeInTheDocument()
    expect(screen.getByText('8 bokstaver, gyldig i 10 minutter.')).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should show the busy button in bokmål', () => {
    renderNorwegian(<PairCodeForm error={undefined} ended={undefined} busy onSubmit={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Kobler til ...' })).toBeDisabled()
  })

  it.each(FORM_ERRORS)('should explain the %s error without English text', (error) => {
    renderNorwegian(<PairCodeForm error={error} ended={undefined} busy={false} onSubmit={vi.fn()} />)

    expect(screen.getByRole('alert')).not.toBeEmptyDOMElement()
    expectNoEnglish()
  })

  it('should name the code length when the code is malformed', () => {
    renderNorwegian(<PairCodeForm error="invalid" ended={undefined} busy={false} onSubmit={vi.fn()} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Sjekk koden. Den har 8 bokstaver.')
  })

  it.each(END_REASONS)('should explain why pairing ended for %s without English text', (ended) => {
    renderNorwegian(<PairCodeForm error={undefined} ended={ended} busy={false} onSubmit={vi.fn()} />)

    expect(screen.getByRole('alert')).not.toBeEmptyDOMElement()
    expectNoEnglish()
  })

  it('should show the paired page without English text', () => {
    renderNorwegian(<PairConnected status={STATUS} onUnpair={vi.fn()} />)

    expect(screen.getByRole('heading', { name: 'Maskinen din er koblet til' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Koble til en annen maskin' })).toBeInTheDocument()
    expectNoEnglish([STATUS.machine])
  })

  it('should show the last run with a Norwegian date and clock', () => {
    renderNorwegian(<PairConnected status={STATUS} onUnpair={vi.fn()} />)

    expect(screen.getByText('Siste kjøring').nextElementSibling?.textContent).toMatch(/^\d{1,2}\. okt kl\. \d{2}:\d{2}, 6 treff$/)
  })

  it('should say no run yet in bokmål', () => {
    renderNorwegian(<PairConnected status={{ ...STATUS, last_run: null }} onUnpair={vi.fn()} />)

    expect(screen.getByText('Ingen kjøring ennå')).toBeInTheDocument()
  })

  it('should show the word check without English text', async () => {
    const { relay, peer } = await startPairing()

    relay.presence(true)

    expect(await screen.findByRole('heading', { name: 'Stemmer disse ordene med terminalen?' })).toBeInTheDocument()
    await waitFor(() => expect(document.title).toBe('Sjekk ordene · jobranger'))
    expectNoEnglish(peer.cli.words)
  })

  it('should wait for the terminal in bokmål after the words match', async () => {
    const { relay, peer, user } = await startPairing()
    relay.presence(true)

    await user.click(await screen.findByRole('button', { name: 'Ja, de stemmer' }))
    await until(() => peer.browserConfirmed)

    expect(await screen.findByRole('heading', { name: 'Venter på terminalen' })).toBeInTheDocument()
    expectNoEnglish(peer.cli.words)
  })

  it('should say it is connecting in bokmål while the handshake runs', async () => {
    await startPairing()

    expect(await screen.findByRole('heading', { name: 'Kobler til maskinen din' })).toBeInTheDocument()
    expectNoEnglish()
  })
})
