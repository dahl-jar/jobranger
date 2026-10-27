import { describe, expect, it } from 'vitest'
import { urlOverride } from '@/shared/config'

const COMMITTED = 'https://relay.example.test'

describe('urlOverride', () => {
  it.each([undefined, ''])('should keep the committed url when the override is %j', (override) => {
    expect(urlOverride(override, COMMITTED)).toBe(COMMITTED)
  })

  it('should use the override when set', () => {
    expect(urlOverride('http://127.0.0.1:5099', COMMITTED)).toBe('http://127.0.0.1:5099')
  })

  it('should accept an https override', () => {
    expect(urlOverride('https://relay.example.test', 'https://other.example.test')).toBe('https://relay.example.test')
  })

  it.each(['http://127.0.0.1:5099/', 'http://127.0.0.1:5099//'])('should drop trailing slashes from %s', (override) => {
    expect(urlOverride(override, COMMITTED)).toBe('http://127.0.0.1:5099')
  })

  it.each(['ftp://relay.example.test', 'relay.example.test', 'javascript:alert(1)'])(
    'should throw when the override %s is not an http url',
    (override) => {
      expect(() => urlOverride(override, COMMITTED)).toThrow('is not an http(s) URL')
    },
  )
})
