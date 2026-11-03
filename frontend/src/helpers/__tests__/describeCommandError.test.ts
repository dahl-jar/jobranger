import { describe, expect, it } from 'vitest'
import { CommandError } from '@/services/relayCommands/commandTracker'
import { describeCommandError } from '@/helpers/describeCommandError'
import { nb } from '@/i18n/nb'

describe('describeCommandError', () => {
  it.each([
    ['a timeout', 'timeout', 'ingen svar fra maskinen din.'],
    ['a dropped connection', 'disconnected', 'forbindelsen ble brutt.'],
  ] as const)('should explain %s in bokmål', (_, code, text) => {
    expect(describeCommandError(new CommandError(code, 'english'), nb)).toBe(text)
  })

  it('should keep the message the machine sent', () => {
    expect(describeCommandError(new CommandError('not_found', 'Rollen finnes ikke.'), nb)).toBe('Rollen finnes ikke.')
  })

  it('should show a thrown string as it is', () => {
    expect(describeCommandError('lost', nb)).toBe('lost')
  })
})
