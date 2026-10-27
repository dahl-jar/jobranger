import { describe, expect, it } from 'vitest'
import { confirmationWords } from '@/features/pairing/confirmationWords'
import { EFF_SHORT_WORDLIST } from '@/features/pairing/effShortWordlist'
import { decodeBase64Url } from '@/features/relay/base64url'
import { SESSION_VECTOR } from '@/test/sessionVector'

describe('confirmationWords', () => {
  it('should derive the four words of the session vector', () => {
    expect(confirmationWords(decodeBase64Url(SESSION_VECTOR.sas))).toEqual(SESSION_VECTOR.words)
  })

  it('should take the lowest base-1296 digit first', () => {
    const sas = new Uint8Array([0, 0, 0, 0, 0, 0, 0x05, 0x11])

    expect(confirmationWords(sas)).toEqual([EFF_SHORT_WORDLIST[1], EFF_SHORT_WORDLIST[1], 'aardvark', 'aardvark'])
  })

  it.each([7, 9])('should throw when the sas has %i bytes', (length) => {
    expect(() => confirmationWords(new Uint8Array(length))).toThrow('sas must be 8 bytes')
  })
})

describe('EFF_SHORT_WORDLIST', () => {
  it('should hold the 1296 words in list order', () => {
    expect([EFF_SHORT_WORDLIST.length, EFF_SHORT_WORDLIST[0], EFF_SHORT_WORDLIST.at(-1)]).toEqual([1296, 'aardvark', 'zucchini'])
  })
})
