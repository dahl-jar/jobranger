import { describe, expect, it } from 'vitest'
import type { ConnectionState } from '@/services/relayConnection/connectionStore'
import { describeConnection } from '@/components/PairPage/describeConnection'
import { nb } from '@/i18n/nb'

describe('describeConnection', () => {
  it.each<[string, ConnectionState, string]>([
    ['the session is starting', { kind: 'handshaking' }, 'Kobler til maskinen din ...'],
    ['the machine is offline', { kind: 'machineOffline', lastSeen: undefined }, 'Maskinen din er ikke på nett'],
    ['the relay is down', { kind: 'relayDown', retryAt: 0 }, 'Får ikke kontakt med mellomtjeneren'],
    ['the browser is offline', { kind: 'networkOffline' }, 'Du er ikke på nett'],
    ['the machine does not know the browser', { kind: 'failed', reason: 'unknown_device' }, 'Maskinen din kjenner ikke denne nettleseren'],
  ])('should describe the connection when %s in bokmål', (_, state, text) => {
    expect(describeConnection(state, nb)).toBe(text)
  })

  it('should give no connection text when ready', () => {
    expect(describeConnection({ kind: 'ready' }, nb)).toBeUndefined()
  })
})
