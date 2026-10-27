import 'fake-indexeddb/auto'
import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { routes } from '@/app/router'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import type { Payload } from '@/features/relay/contract/payload.gen'
import { RelayDepsContext } from '@/features/relay/relayContext'
import { LanguageProvider } from '@/shared/i18n/LanguageProvider'
import { SNAPSHOTS } from '@/test/appData'
import { CliPeer, FakeRelay } from '@/test/fakeRelay'
import { until } from '@/test/flush'
import { relayDeps, storedPairing } from '@/test/relayFixtures'
import { warmRoutes } from '@/test/warmRoutes'

const CV_BYTES = 1024 * 1024
const CV_CHUNKS = 43
const UPLOAD_TEST_MS = 20_000
const UPLOAD_SENT_MS = 15_000

let unmount: (() => void) | undefined

afterEach(() => {
  unmount?.()
  unmount = undefined
})

async function renderPaired(path: string) {
  const relay = new FakeRelay()
  const peer = await CliPeer.attach(relay)
  const pairing = await storedPairing({ cliStaticPublicKey: peer.cli.staticPublic })
  const deps = relayDeps(relay, { loadPairing: async () => pairing, clearPairingOf: async () => undefined })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(
    <LanguageProvider>
      <ThemeProvider>
        <QueryClientProvider client={createQueryClient()}>
          <RelayDepsContext value={deps}>
            <RouterProvider router={router} />
          </RelayDepsContext>
        </QueryClientProvider>
      </ThemeProvider>
    </LanguageProvider>,
  )
  unmount = view.unmount
  await until(() => relay.events !== undefined)
  relay.presence(true)
  await until(() => peer.browserConfirmed)
  for (const snapshot of Object.values(SNAPSHOTS)) await peer.sendData(JSON.stringify(snapshot))
  return { relay, peer, router, user: userEvent.setup() }
}

function receivedTypes(peer: CliPeer): string[] {
  return peer.received.map((text) => (JSON.parse(text) as Payload).type)
}

describe('AppLayout with the relay', () => {
  warmRoutes(routes)

  it('should show the matches the machine sends after the handshake', async () => {
    await renderPaired('/app/active')

    const list = await screen.findByRole('navigation', { name: 'Lists' })

    expect(within(list).getByRole('link', { name: /^Active/ })).toHaveTextContent(`Active ${SNAPSHOTS.ads.active_total}`)
  })

  it('should show the upload progress of a 1 MB CV sent as 43 chunks', async () => {
    const { peer, user } = await renderPaired('/app/settings/profile')
    const picker = await screen.findByLabelText(/Replace CV/)

    await user.upload(picker, new File([new Uint8Array(CV_BYTES)], 'cv.pdf', { type: 'application/pdf' }))

    await waitFor(() => expect(peer.received.length).toBeGreaterThanOrEqual(CV_CHUNKS), { timeout: UPLOAD_SENT_MS })
    expect([receivedTypes(peer), await screen.findByText('Sending 100%')]).toEqual([Array.from({ length: CV_CHUNKS }, () => 'chunk'), expect.anything()])
  }, UPLOAD_TEST_MS)
})
