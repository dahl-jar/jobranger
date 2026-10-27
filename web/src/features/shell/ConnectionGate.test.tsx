import { type QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { AddApplicationForm } from '@/features/applications/AddApplicationForm'
import { disconnectedError } from '@/features/relay/commands'
import { QUERY_KEYS } from '@/app/queryKeys'
import { type ConnectionState, type ConnectionStore, createConnectionStore } from '@/features/relay/connectionStore'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { type Relay, RelayContext } from '@/features/relay/relayContext'
import { ConnectionGate } from '@/features/shell/ConnectionGate'
import { useEditsDisabled } from '@/features/shell/useEditsDisabled'

const SEEN = new Date(2026, 9, 13, 14, 2).toISOString()
const NOW = new Date(2026, 9, 13, 16, 30)
const RETRY_IN_MS = 30_000

function ListProbe() {
  return <p>{useEditsDisabled() ? 'list, edits off' : 'list, edits on'}</p>
}

function PairProbe() {
  const { state } = useLocation() as { state: { reason?: string } | null }
  return <p>pair page {state?.reason ?? 'no reason'}</p>
}

interface Rendered {
  queryClient: QueryClient
  retryNow: ReturnType<typeof vi.fn>
  unpair: ReturnType<typeof vi.fn>
  store: ConnectionStore
}

function renderGate(state: ConnectionState, { withList = false, page = <ListProbe /> } = {}): Rendered {
  const store = createConnectionStore()
  store.dispatch(state)
  const retryNow = vi.fn()
  const unpair = vi.fn(async () => undefined)
  const relay: Relay = { store, send: vi.fn(), unpair, retryNow }
  const queryClient = createQueryClient()
  if (withList) queryClient.setQueryData(QUERY_KEYS.ads, payloadFixtures['ads.snapshot'])
  const router = createMemoryRouter(
    [
      {
        path: '/app/today',
        element: (
          <ConnectionGate>{page}</ConnectionGate>
        ),
      },
      { path: '/pair', element: <PairProbe /> },
    ],
    { initialEntries: ['/app/today'] },
  )
  render(
    <QueryClientProvider client={queryClient}>
      <RelayContext value={relay}>
        <RouterProvider router={router} />
      </RelayContext>
    </QueryClientProvider>,
  )
  return { queryClient, retryNow, unpair, store }
}

afterEach(() => {
  vi.useRealTimers()
})

describe('ConnectionGate', () => {
  it('should say it is connecting while the session starts', () => {
    renderGate({ kind: 'handshaking' })

    expect(screen.getByRole('status')).toHaveTextContent('Connecting to your machine...')
  })

  it('should send an unpaired browser to the pair page with the reason', async () => {
    renderGate({ kind: 'unpaired', reason: 'ticket' })

    expect(await screen.findByText('pair page ticket')).toBeInTheDocument()
  })

  it('should count down to the next relay attempt', async () => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] })
    vi.setSystemTime(NOW)
    renderGate({ kind: 'relayDown', retryAt: NOW.getTime() + RETRY_IN_MS })
    const first = screen.getByText(/Can't reach the relay/).textContent

    act(() => vi.advanceTimersByTime(1000))

    expect([first, screen.getByText(/Can't reach the relay/).textContent]).toEqual([
      "Can't reach the relay. Trying again in 30 s.",
      "Can't reach the relay. Trying again in 29 s.",
    ])
  })

  it('should retry the relay on Try now', async () => {
    const { retryNow } = renderGate({ kind: 'relayDown', retryAt: Date.now() + RETRY_IN_MS })

    await userEvent.click(screen.getByRole('button', { name: 'Try now' }))

    expect(retryNow).toHaveBeenCalledOnce()
  })

  it.each([
    ['the handshake', 'handshake', 'The connection check failed. Pair again.'],
    ['the machine key', 'cli_key', 'The connection check failed. Pair again.'],
    ['the browser key', 'unknown_device', "Your machine doesn't know this browser. Pair again."],
  ] as const)('should ask to pair again when %s check fails', (_, reason, text) => {
    renderGate({ kind: 'failed', reason })

    expect(screen.getByRole('heading').textContent).toBe(text)
  })

  it('should unpair once on Pair again', async () => {
    const { unpair } = renderGate({ kind: 'failed', reason: 'unknown_device' })

    await userEvent.click(screen.getByRole('button', { name: 'Pair again' }))

    expect(unpair).toHaveBeenCalledOnce()
  })

  it('should show the offline screen with the last time it was seen', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(NOW)

    renderGate({ kind: 'machineOffline', lastSeen: SEEN })

    expect([screen.getByRole('heading').textContent, screen.getByText(/^Last seen/).textContent, screen.getByText(/^Run/).textContent]).toEqual([
      'Your machine is offline',
      'Last seen today at 14:02.',
      'Run jobranger serve on it. This page connects when it comes online.',
    ])
  })

  it('should say the browser is offline before any list loaded', () => {
    renderGate({ kind: 'networkOffline' })

    expect([screen.getByRole('heading').textContent, screen.getByText(/network is back/).textContent]).toEqual([
      "You're offline",
      'This page connects when your network is back.',
    ])
  })

  it('should stop the countdown once the relay is back', () => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] })
    const { store } = renderGate({ kind: 'relayDown', retryAt: Date.now() + RETRY_IN_MS })

    act(() => store.dispatch({ kind: 'ready' }))

    expect(vi.getTimerCount()).toBe(0)
  })

  it('should leave out the last seen line when it is unknown', () => {
    renderGate({ kind: 'machineOffline', lastSeen: undefined })

    expect([screen.getByRole('heading').textContent, screen.queryByText(/Last seen/)]).toEqual(['Your machine is offline', null])
  })

  it.each<[string, ConnectionState, string]>([
    ['the machine is offline', { kind: 'machineOffline', lastSeen: SEEN }, 'Your machine went offline at 14:02. Changes are paused.'],
    ['the machine went offline at an unknown time', { kind: 'machineOffline', lastSeen: undefined }, 'Your machine went offline. Changes are paused.'],
    ['the relay is down', { kind: 'relayDown', retryAt: 0 }, "Can't reach the relay. Changes are paused."],
    ['the session reconnects', { kind: 'connecting' }, 'Reconnecting to your machine. Changes are paused.'],
    ['the browser is offline', { kind: 'networkOffline' }, "You're offline. Changes are paused."],
  ])('should keep a loaded list read-only when %s', (_, state, notice) => {
    renderGate(state, { withList: true })

    expect([screen.getByRole('status').textContent, screen.getByText(/^list/).textContent]).toEqual([notice, 'list, edits off'])
  })

  it('should say the list is loading when ready before the first snapshot', () => {
    renderGate({ kind: 'ready' })

    expect(screen.getByRole('status')).toHaveTextContent('Loading your list...')
  })

  it('should keep "Not saved" when the connection drops during a save', async () => {
    const user = userEvent.setup()
    let failSave: (error: Error) => void = () => undefined
    const pending = new Promise<void>((_, reject) => {
      failSave = reject
    })
    const page = <AddApplicationForm onAdd={() => pending} onCancel={vi.fn()} disabled={false} />
    const { store } = renderGate({ kind: 'ready' }, { withList: true, page })
    await user.type(screen.getByLabelText('Company'), 'Org Three AS')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    await act(async () => {
      store.dispatch({ kind: 'connecting' })
      failSave(disconnectedError())
    })

    expect(screen.getByLabelText('Company')).toHaveValue('Org Three AS')
    expect(screen.getByRole('alert')).toHaveTextContent('Not saved: connection lost.')
  })

  it('should show the list with edits on when ready', () => {
    renderGate({ kind: 'ready' }, { withList: true })

    expect([screen.getByText(/^list/).textContent, screen.queryByRole('status')]).toEqual(['list, edits on', null])
  })
})
