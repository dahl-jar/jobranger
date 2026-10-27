import { describe, expect, it } from 'vitest'
import { encodeBase64Url } from '@/features/relay/base64url'
import { HELLO_MAX_BYTES } from '@/features/relay/contract/limits'
import { encodeHello, HelloError, parseHello, validateAgent } from '@/features/relay/hello'
import { readHello } from '@/test/fakeRelay'

const KEY_A = encodeBase64Url(new Uint8Array(32).fill(1))
const KEY_B = encodeBase64Url(new Uint8Array(32).fill(2))
const OFFER = `{"t":"offer","cli_static":"${KEY_A}","cli_eph":"${KEY_B}"}`

function wire(text: string): string {
  return encodeBase64Url(new TextEncoder().encode(text))
}

function padded(json: string, length: number): string {
  return json.slice(0, -1) + ' '.repeat(length - json.length) + '}'
}

describe('parseHello', () => {
  it('should read the key bytes of an offer', () => {
    expect(parseHello(wire(OFFER))).toEqual({
      t: 'offer',
      cliStatic: new Uint8Array(32).fill(1),
      cliEph: new Uint8Array(32).fill(2),
    })
  })

  it.each(['abort', 'unknown_session', 'unknown_device'])('should read the %s step', (step) => {
    expect(parseHello(wire(`{"t":"${step}"}`))).toEqual({ t: step })
  })

  it('should accept a step of exactly HELLO_MAX_BYTES', () => {
    expect(parseHello(wire(padded(OFFER, HELLO_MAX_BYTES))).t).toBe('offer')
  })

  it.each([
    { rule: 'over HELLO_MAX_BYTES', text: padded(OFFER, HELLO_MAX_BYTES + 1) },
    { rule: 'a repeated key', text: `{"t":"abort","t":"abort"}` },
    { rule: 'an unknown step', text: `{"t":"hello"}` },
    { rule: 'a missing field', text: `{"t":"offer","cli_static":"${KEY_A}"}` },
    { rule: 'an extra field', text: `{"t":"abort","reason":"x"}` },
    { rule: 'a __proto__ field', text: `{"t":"abort","__proto__":{}}` },
    { rule: 'a field of the wrong JSON type', text: `{"t":"confirm","mac":32}` },
    { rule: 'a step name of the wrong JSON type', text: `{"t":["abort"]}` },
    { rule: 'a 31-byte key', text: `{"t":"offer","cli_static":"${encodeBase64Url(new Uint8Array(31))}","cli_eph":"${KEY_B}"}` },
    { rule: 'a padded base64url field', text: `{"t":"confirm","mac":"${KEY_A}="}` },
    { rule: 'an array', text: '[{"t":"abort"}]' },
    { rule: 'null', text: 'null' },
    { rule: 'broken JSON', text: '{"t":"abort"' },
    { rule: 'a byte order mark', text: '﻿{"t":"abort"}' },
  ])('should reject $rule', ({ text }) => {
    expect(() => parseHello(wire(text))).toThrow(HelloError)
  })

  it('should reject bytes that are not UTF-8', () => {
    const bytes = new Uint8Array([...new TextEncoder().encode('{"t":"abort'), 0xff, 0x22, 0x7d])

    expect(() => parseHello(encodeBase64Url(bytes))).toThrow(HelloError)
  })

  it('should reject a ciphertext that is not canonical base64url', () => {
    expect(() => parseHello(`${wire('{"t":"abort"}')}=`)).toThrow(HelloError)
  })
})

describe('encodeHello', () => {
  it('should write pair_tag on a commit that has one', () => {
    const commit = { t: 'commit', browserStatic: new Uint8Array(32).fill(1), commit: new Uint8Array(32).fill(2), pairTag: new Uint8Array(16).fill(1) } as const

    expect(readHello(encodeHello(commit))).toEqual({
      t: 'commit',
      browser_static: KEY_A,
      commit: KEY_B,
      pair_tag: encodeBase64Url(new Uint8Array(16).fill(1)),
    })
  })

  it('should write no pair_tag on a commit without one', () => {
    const commit = { t: 'commit', browserStatic: new Uint8Array(32).fill(1), commit: new Uint8Array(32).fill(2) } as const

    expect(Object.keys(readHello(encodeHello(commit)))).toEqual(['t', 'browser_static', 'commit'])
  })

  it('should write binary fields as base64url under their wire names', () => {
    const step = readHello(encodeHello({ t: 'confirm', mac: new Uint8Array(32).fill(1) }))

    expect(step).toEqual({ t: 'confirm', mac: KEY_A })
  })

  it('should write the reveal agent as a JSON string', () => {
    const encoded = encodeHello({ t: 'reveal', browserEph: new Uint8Array(32), nonceB: new Uint8Array(16), agent: 'Firefox on macOS' })

    expect(readHello(encoded)).toMatchObject({ agent: 'Firefox on macOS' })
  })
})

describe('validateAgent', () => {
  it('should accept an agent of 256 UTF-8 bytes', () => {
    expect(validateAgent('é'.repeat(128))).toHaveLength(256)
  })

  it('should accept an agent with a surrogate pair', () => {
    expect(validateAgent('Firefox \uD83D\uDE00 on macOS')).toHaveLength(21)
  })

  it.each([
    { rule: 'over 256 UTF-8 bytes', agent: `${'é'.repeat(128)}a` },
    { rule: 'a lone high surrogate at the end', agent: 'Firefox \uD800' },
    { rule: 'a lone high surrogate before a letter', agent: 'Firefox \uD800x' },
    { rule: 'a lone low surrogate', agent: 'Firefox x\uDC00' },
  ])('should reject an agent with $rule', ({ agent }) => {
    expect(() => validateAgent(agent)).toThrow(HelloError)
  })
})
