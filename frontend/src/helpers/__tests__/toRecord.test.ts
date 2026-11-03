import { describe, expect, it } from 'vitest'
import { toRecord } from '@/helpers/toRecord'

describe('toRecord', () => {
  it('should return the own fields of an object', () => {
    expect(toRecord({ channel: 'one' })).toEqual({ channel: 'one' })
  })

  it.each([null, undefined, 'text', 3])('should return undefined for %j', (value) => {
    expect(toRecord(value)).toBeUndefined()
  })
})
