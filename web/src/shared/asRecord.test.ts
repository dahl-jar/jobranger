import { describe, expect, it } from 'vitest'
import { asRecord } from '@/shared/asRecord'

describe('asRecord', () => {
  it('should return the own fields of an object', () => {
    expect(asRecord({ channel: 'one' })).toEqual({ channel: 'one' })
  })

  it.each([null, undefined, 'text', 3])('should return undefined for %j', (value) => {
    expect(asRecord(value)).toBeUndefined()
  })
})
