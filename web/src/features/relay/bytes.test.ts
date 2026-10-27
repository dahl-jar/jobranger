import { describe, expect, it } from 'vitest'
import { concatBytes, sameBytes } from '@/features/relay/bytes'

describe('concatBytes', () => {
  it('should join the parts in order', () => {
    expect(concatBytes(new Uint8Array([1, 2]), new Uint8Array(), new Uint8Array([3]))).toEqual(new Uint8Array([1, 2, 3]))
  })
})

describe('sameBytes', () => {
  it('should match equal bytes', () => {
    expect(sameBytes(new Uint8Array([1, 2]), new Uint8Array([1, 2]))).toBe(true)
  })

  it.each([
    { name: 'a different byte', right: [1, 3] },
    { name: 'a shorter array', right: [1] },
  ])('should reject $name', ({ right }) => {
    expect(sameBytes(new Uint8Array([1, 2]), new Uint8Array(right))).toBe(false)
  })
})
