import { RelayUnreachableError } from '@/features/relay/handleClient'
import { clearPairingOf, generateWebKeys, loadPairing, type StoredPairing } from '@/features/relay/keyStore'
import type { SeenStorage } from '@/features/relay/lastSeen'
import type { RelayDeps } from '@/features/relay/relayContext'
import type { ClaimResult } from '@/features/pairing/claimPairing'
import { type CliPeer, type FakeRelay, TEST_CHANNEL } from '@/test/fakeRelay'
import { until } from '@/test/flush'

export const LONG_DELAY_MS = 60_000
export const TEST_AGENT = 'Firefox on macOS'
export const LAST_SEEN_KEY = 'jobranger.lastSeen'
export const SEEN_AT = '2026-10-13T14:02:00.000Z'
export const CLAIM_OK: ClaimResult = {
  kind: 'ok',
  channel: TEST_CHANNEL,
  handle: 'handle-one',
  ticket: 'ticket-one',
  ticketExpiresAt: '2027-01-11T08:00:00Z',
}
const CLI_KEY_BYTES = 32

export async function storedPairing(overrides: Partial<StoredPairing> = {}): Promise<StoredPairing> {
  return {
    channel: TEST_CHANNEL,
    ticket: 'ticket-one',
    ticketExpiresAt: '2027-01-11T08:00:00Z',
    pending: false,
    cliStaticPublicKey: new Uint8Array(CLI_KEY_BYTES),
    webStatic: await generateWebKeys(),
    pairedAt: '2026-10-13T08:00:00Z',
    ...overrides,
  }
}

export function withoutCliKey(pairing: StoredPairing): StoredPairing {
  const copy = { ...pairing }
  delete copy.cliStaticPublicKey
  return copy
}

export function relayDeps(relay: FakeRelay, overrides: Partial<RelayDeps> = {}): RelayDeps {
  return {
    loadPairing,
    clearPairingOf,
    fetchHandle: async () => 'handle-one',
    connect: relay.connect,
    agent: TEST_AGENT,
    retryDelay: () => LONG_DELAY_MS,
    paceClock: relay.clock,
    ...overrides,
  }
}

export function memoryStorage(entries: Record<string, string> = {}): SeenStorage & { entries: Record<string, string> } {
  return {
    entries,
    getItem: (key) => entries[key] ?? null,
    setItem: (key, value) => {
      entries[key] = value
    },
    removeItem: (key) => {
      Reflect.deleteProperty(entries, key)
    },
  }
}

export async function answerUnknownDevice(relay: FakeRelay, peer: CliPeer): Promise<void> {
  await until(() => relay.events !== undefined)
  relay.presence(true)
  await until(() => relay.sent.length > 0)
  peer.sendHello({ t: 'unknown_device' }, relay.sent[0]?.sid)
}

export function failingFirstHandle(): () => Promise<string> {
  let calls = 0
  return async () => {
    calls += 1
    if (calls === 1) throw new RelayUnreachableError('offline')
    return 'handle-two'
  }
}
