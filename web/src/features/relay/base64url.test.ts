import { describe, expect, it } from 'vitest'
import { decodeBase64Url, encodeBase64, encodeBase64Url } from '@/features/relay/base64url'

const BYTES = new Uint8Array([0xfb, 0xff, 0xbf, 0x00, 0x01])

describe('encodeBase64Url', () => {
  it('should use the url alphabet without padding', () => {
    expect(encodeBase64Url(BYTES)).toBe('-_-_AAE')
  })
})

describe('encodeBase64', () => {
  it('should use the standard alphabet with padding', () => {
    expect(encodeBase64(BYTES)).toBe('+/+/AAE=')
  })
})

describe('decodeBase64Url', () => {
  it('should decode url alphabet text', () => {
    expect(decodeBase64Url('-_-_AAE')).toEqual(BYTES)
  })

  it('should decode an empty string to no bytes', () => {
    expect(decodeBase64Url('')).toEqual(new Uint8Array())
  })

  it.each([
    { name: 'standard alphabet', text: '+/+/AAE' },
    { name: 'a length of 1 mod 4', text: 'AAAAA' },
    { name: 'non-zero spare bits', text: 'AAF' },
  ])('should reject $name', ({ text }) => {
    expect(() => decodeBase64Url(text)).toThrow('not canonical base64url')
  })
})
