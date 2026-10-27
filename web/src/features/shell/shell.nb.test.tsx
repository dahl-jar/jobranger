import { QueryClientProvider } from '@tanstack/react-query'
import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { QUERY_KEYS } from '@/app/queryKeys'
import { CommandError } from '@/features/relay/commands'
import { type ConnectionState, createConnectionStore } from '@/features/relay/connectionStore'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { type Relay, RelayContext } from '@/features/relay/relayContext'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { ConnectionGate } from '@/features/shell/ConnectionGate'
import { connectionText } from '@/features/shell/connectionText'
import { nb } from '@/shared/i18n/nb'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian } from '@/test/render'

const SEEN = new Date(2026, 9, 13, 14, 2).toISOString()
const NOW = new Date(2026, 9, 13, 16, 30)
const RETRY_IN_MS = 30_000
const LIST_TEXT = 'liste'

function renderGate(state: ConnectionState, { withList = false } = {}) {
  const store = createConnectionStore()
  store.dispatch(state)
  const relay: Relay = { store, send: vi.fn(), unpair: vi.fn(async () => undefined), retryNow: vi.fn() }
  const queryClient = createQueryClient()
  if (withList) queryClient.setQueryData(QUERY_KEYS.ads, payloadFixtures['ads.snapshot'])
  renderNorwegian(
    <QueryClientProvider client={queryClient}>
      <RelayContext value={relay}>
        <ConnectionGate>
          <p>{LIST_TEXT}</p>
        </ConnectionGate>
      </RelayContext>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.useRealTimers()
})

describe('connection screens in bokmål', () => {
  it.each<[string, ConnectionState, string]>([
    ['connecting', { kind: 'handshaking' }, 'Kobler til maskinen din ...'],
    ['loading the list', { kind: 'ready' }, 'Laster listen din ...'],
  ])('should say it is %s in bokmål', (_, state, text) => {
    renderGate(state)

    expect(screen.getByRole('status')).toHaveTextContent(text)
    expectNoEnglish()
  })

  it('should count down to the next relay attempt in bokmål', () => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] })
    vi.setSystemTime(NOW)

    renderGate({ kind: 'relayDown', retryAt: NOW.getTime() + RETRY_IN_MS })

    expect(screen.getByRole('status')).toHaveTextContent('Får ikke kontakt med mellomtjeneren. Prøver igjen om 30 s.')
    expect(screen.getByRole('button', { name: 'Prøv nå' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it.each([
    ['the handshake', 'handshake', 'Kontrollen av tilkoblingen feilet. Koble til på nytt.'],
    ['the machine key', 'cli_key', 'Kontrollen av tilkoblingen feilet. Koble til på nytt.'],
    ['the browser key', 'unknown_device', 'Maskinen din kjenner ikke denne nettleseren. Koble til på nytt.'],
  ] as const)('should ask to pair again in bokmål when %s check fails', (_, reason, heading) => {
    renderGate({ kind: 'failed', reason })

    expect(screen.getByRole('heading').textContent).toBe(heading)
    expectNoEnglish()
  })

  it('should show the offline screen with the last time seen today', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(NOW)

    renderGate({ kind: 'machineOffline', lastSeen: SEEN })

    expect(screen.getByRole('heading').textContent).toBe('Maskinen din er ikke på nett')
    expect(screen.getByText(/^Sist sett/).textContent).toBe('Sist sett i dag kl. 14:02.')
    expect(screen.getByText(/^Kjør/).textContent).toBe('Kjør jobranger serve på maskinen. Siden kobler seg til når maskinen er på nett.')
    expectNoEnglish()
  })

  it('should leave out the last seen line in bokmål when it is unknown', () => {
    renderGate({ kind: 'machineOffline', lastSeen: undefined })

    expect(screen.queryByText(/Sist sett/)).not.toBeInTheDocument()
    expectNoEnglish()
  })

  it('should say the browser is offline in bokmål', () => {
    renderGate({ kind: 'networkOffline' })

    expect(screen.getByRole('heading').textContent).toBe('Du er ikke på nett')
    expectNoEnglish()
  })

  it.each<[string, ConnectionState, string]>([
    ['the machine is offline', { kind: 'machineOffline', lastSeen: SEEN }, 'Maskinen din gikk av nett kl. 14:02. Endringer står på pause.'],
    ['the machine went offline at an unknown time', { kind: 'machineOffline', lastSeen: undefined }, 'Maskinen din gikk av nett. Endringer står på pause.'],
    ['the relay is down', { kind: 'relayDown', retryAt: 0 }, 'Får ikke kontakt med mellomtjeneren. Endringer står på pause.'],
    ['the session reconnects', { kind: 'connecting' }, 'Kobler til maskinen din på nytt. Endringer står på pause.'],
    ['the browser is offline', { kind: 'networkOffline' }, 'Du er ikke på nett. Endringer står på pause.'],
  ])('should explain a read-only list in bokmål when %s', (_, state, notice) => {
    renderGate(state, { withList: true })

    expect(screen.getByRole('status').textContent).toBe(notice)
    expect(screen.getByText(LIST_TEXT)).toBeInTheDocument()
    expectNoEnglish()
  })
})

describe('connectionText', () => {
  it.each<[string, ConnectionState, string]>([
    ['the session is starting', { kind: 'handshaking' }, 'Kobler til maskinen din ...'],
    ['the machine is offline', { kind: 'machineOffline', lastSeen: undefined }, 'Maskinen din er ikke på nett'],
    ['the relay is down', { kind: 'relayDown', retryAt: 0 }, 'Får ikke kontakt med mellomtjeneren'],
    ['the browser is offline', { kind: 'networkOffline' }, 'Du er ikke på nett'],
    ['the machine does not know the browser', { kind: 'failed', reason: 'unknown_device' }, 'Maskinen din kjenner ikke denne nettleseren'],
  ])('should describe the connection when %s in bokmål', (_, state, text) => {
    expect(connectionText(state, nb)).toBe(text)
  })

  it('should give no connection text when ready', () => {
    expect(connectionText({ kind: 'ready' }, nb)).toBeUndefined()
  })
})

describe('commandErrorText', () => {
  it.each([
    ['a timeout', 'timeout', 'ingen svar fra maskinen din.'],
    ['a dropped connection', 'disconnected', 'forbindelsen ble brutt.'],
  ] as const)('should explain %s in bokmål', (_, code, text) => {
    expect(commandErrorText(new CommandError(code, 'english'), nb)).toBe(text)
  })

  it('should keep the message the machine sent', () => {
    expect(commandErrorText(new CommandError('not_found', 'Rollen finnes ikke.'), nb)).toBe('Rollen finnes ikke.')
  })

  it('should show a thrown string as it is', () => {
    expect(commandErrorText('lost', nb)).toBe('lost')
  })
})
