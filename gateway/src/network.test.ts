import { describe, expect, it } from 'vitest'
import { clientKeys } from './network.ts'

function sameness(first: string, second: string): string {
  return first === second ? 'same' : 'different'
}

describe('clientKeys', () => {
  it.each([
    ['192.0.2.255', '192.0.2.1', 'different', 'same'],
    ['10.1.11.1', '10.11.1.1', 'different', 'different'],
    ['192.0.2.1', '192.0.3.1', 'different', 'different'],
    ['2001:db8:1:2::1', '2001:db8:1:3::1', 'different', 'same'],
    ['2001:db8:1::1', '2001:db8:2::1', 'different', 'different'],
    ['2001:db8:1:11::1', '2001:db8:11:1::1', 'different', 'different'],
    ['2001::1:2:3:4:5', '2001:0:0:1::', 'same', 'same'],
    ['2001:db8:1:2:3:4:5:6', '2001:db8:1:2::9', 'same', 'same'],
    ['2001:DB8:1:2::1', '2001:0db8:0001:0002:0:0:0:1', 'same', 'same'],
    ['::ffff:192.0.2.1', '192.0.2.1', 'same', 'same'],
    ['192.0.2.1', '::c000:201', 'different', 'different'],
  ])('should key %s and %s to %s clients and %s networks', (first, second, client, network) => {
    const a = clientKeys(first)
    const b = clientKeys(second)

    expect({ client: sameness(a.client, b.client), network: sameness(a.network, b.network) }).toEqual({ client, network })
  })

  it.each(['', '192.0.2', '192.0.2.256', '192.0.2.1.5', '192.0.2.01', '2001:db8::1::2', '::1]/x[::1'])(
    'should put unparseable address %j in the shared unknown bucket',
    (address) => {
      expect(clientKeys(address)).toEqual(clientKeys(null))
    },
  )
})
