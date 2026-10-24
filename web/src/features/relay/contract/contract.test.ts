import { describe, expect, it } from 'vitest'
import { ENVELOPE_INVALID_COUNT, PAYLOAD_INVALID_COUNT } from '@/test/fixtureCounts'
import { envelopeFixtures, payloadFixtures } from './fixtures.gen'
import { MAX_INTEGER_DIGITS, MAX_JSON_DEPTH } from './limits'
import { ContractError, parseEnvelope, parsePayload, parseTimestamp } from './validate'

const invalidPayloads = Object.entries(
  import.meta.glob<unknown>('@contracts/fixtures/payload-invalid/*.json', {
    eager: true,
    import: 'default',
  }),
)
const invalidEnvelopes = Object.entries(
  import.meta.glob<unknown>('@contracts/fixtures/envelope-invalid/*.json', {
    eager: true,
    import: 'default',
  }),
)
const OVERSIZE_EMAIL = `a@${'b.'.repeat(4_000_000)}cc`
const adMarkText = JSON.stringify(payloadFixtures['ad.mark'])
const nested = (depth: number) => `${'['.repeat(depth)}${']'.repeat(depth)}`
const withExtra = (extra: string) => adMarkText.replace('"v":1,', `"v":1,"extra":${extra},`)
const rejection = (errors: string[], messageType?: string) =>
  expect.objectContaining({ name: 'ContractError', errors, messageType })

describe('parsePayload', () => {
  it.each(Object.entries(payloadFixtures))('should accept payload fixture %s', (_, fixture) => {
    expect(parsePayload(JSON.stringify(fixture))).toEqual(fixture)
  })

  it('should find every invalid payload fixture', () => {
    expect(invalidPayloads).toHaveLength(PAYLOAD_INVALID_COUNT)
  })

  it.each(invalidPayloads)('should reject invalid payload %s', (_, raw) => {
    expect(() => parsePayload(JSON.stringify(raw))).toThrow(ContractError)
  })

  it('should return unknown fields unchanged', () => {
    const raw = { ...payloadFixtures['ad.mark'], extra: 1 }

    expect(parsePayload(JSON.stringify(raw))).toEqual(raw)
  })

  it.each([
    [
      'bad field',
      JSON.stringify({ ...payloadFixtures['ad.mark'], mark: 'wrong' }),
      rejection(['/mark must be equal to one of the allowed values'], 'ad.mark'),
    ],
    ['top-level string', '"abc"', rejection(['/ must be object'])],
    ['null', 'null', rejection(['/ must be object'])],
    ['non-string type', JSON.stringify({ type: 5, v: 1 }), rejection(['/ tag "type" must be string'])],
  ])('should name the failing field and message type for %s', (_, text, error) => {
    expect(() => parsePayload(text)).toThrow(error)
  })

  it('should reject an oversize email address before its pattern runs', () => {
    const snapshot = payloadFixtures['config.snapshot']
    const raw = {
      ...snapshot,
      notify: { ...snapshot.notify, email: { on: true, address: OVERSIZE_EMAIL } },
    }

    expect(() => parsePayload(JSON.stringify(raw))).toThrow(ContractError)
  })

  it.each([
    ['duplicate key', adMarkText.replace('"v":1,', '"v":1,"mark":"applied",'), 'duplicate object key'],
    ['duplicate escaped key', adMarkText.replace('"v":1,', '"v":1,"\\u006dark":"applied",'), 'duplicate object key'],
    ['duplicate first key', withExtra('{"a":1,"a":2}'), 'duplicate object key'],
    ['invalid key escape', withExtra('{"\\x":1}'), 'object key is not a JSON string'],
    ['unterminated string', '{"a":"abc', 'unterminated string'],
    ['nesting over max depth', withExtra(nested(MAX_JSON_DEPTH)), `nesting deeper than ${MAX_JSON_DEPTH}`],
    [
      'integer over max digits',
      withExtra('1'.repeat(MAX_INTEGER_DIGITS + 1)),
      `integer longer than ${MAX_INTEGER_DIGITS} digits`,
    ],
    ['lone surrogate', withExtra('"\\ud800"'), 'string holds a lone surrogate', 'ad.mark'],
    ['lone surrogate in array', withExtra('["\\ud800"]'), 'string holds a lone surrogate', 'ad.mark'],
    ['lone surrogate key', withExtra('{"\\ud800":1}'), 'key holds a lone surrogate', 'ad.mark'],
    ['infinite number', withExtra('1e400'), 'non-finite number', 'ad.mark'],
  ])('should reject %s', (_, text, problem, messageType?: string) => {
    expect(() => parsePayload(text)).toThrow(rejection([problem], messageType))
  })

  it('should name the JSON syntax error', () => {
    expect(() => parsePayload(`${adMarkText},`)).toThrow('contract violation: invalid JSON: ')
  })

  it.each([
    ['nesting at max depth', withExtra(nested(MAX_JSON_DEPTH - 1))],
    ['integer at max digits', withExtra(`-${'9'.repeat(MAX_INTEGER_DIGITS)}`)],
    ['fraction with long mantissa', withExtra(`0.${'1'.repeat(MAX_INTEGER_DIGITS + 1)}`)],
    ['brackets inside a string', withExtra(`"${nested(MAX_JSON_DEPTH)}"`)],
    ['brackets after an escaped quote', withExtra(`"\\"${nested(MAX_JSON_DEPTH)}"`)],
    ['sibling arrays past max depth', withExtra(`[${'[],'.repeat(MAX_JSON_DEPTH)}[]]`)],
    ['sibling objects past max depth', withExtra(`[${'{},'.repeat(MAX_JSON_DEPTH)}{}]`)],
    ['repeated strings in an array', withExtra('["a","b","b"]')],
    ['surrogate pair', withExtra('"\\ud83d\\ude00"')],
  ])('should accept %s', (_, text) => {
    expect(parsePayload(text)).toMatchObject(payloadFixtures['ad.mark'])
  })
})

describe('parseEnvelope', () => {
  it.each(Object.entries(envelopeFixtures))('should accept envelope fixture %s', (_, fixture) => {
    expect(parseEnvelope(JSON.stringify(fixture))).toEqual(fixture)
  })

  it('should find every invalid envelope fixture', () => {
    expect(invalidEnvelopes).toHaveLength(ENVELOPE_INVALID_COUNT)
  })

  it.each(invalidEnvelopes)('should reject invalid envelope %s', (_, raw) => {
    expect(() => parseEnvelope(JSON.stringify(raw))).toThrow(ContractError)
  })

  it('should name the kind when an envelope is rejected', () => {
    const text = JSON.stringify({ ...envelopeFixtures.data, seq: 2 ** 53 })

    expect(() => parseEnvelope(text)).toThrow(rejection(['/seq must be <= 9007199254740991'], 'data'))
  })

  it('should reject a duplicate kind', () => {
    const text = JSON.stringify(envelopeFixtures.data).replace('"kind":"data"', '"kind":"presence","kind":"data"')

    expect(() => parseEnvelope(text)).toThrow(ContractError)
  })

  it('should reject an infinite number in an unknown field', () => {
    const text = JSON.stringify(envelopeFixtures.data).replace('"v":1,', '"v":1,"x":1e400,')

    expect(() => parseEnvelope(text)).toThrow(ContractError)
  })
})

describe('parseTimestamp', () => {
  it('should parse a timestamp with offset and fraction', () => {
    expect(parseTimestamp('2026-10-09T03:00:00.5+02:00')).toEqual(
      new Date(Date.UTC(2026, 9, 9, 1, 0, 0, 500)),
    )
  })

  it('should parse the first year', () => {
    expect(parseTimestamp('0001-01-01T00:00:00Z').getUTCFullYear()).toBe(1)
  })

  it.each([
    ['impossible date', '2026-02-30T00:00:00Z', 'timestamp is not a calendar date'],
    ['year zero', '0000-01-01T00:00:00Z', 'timestamp is not a calendar date'],
    ['space separator', '2026-10-09 03:00:00Z', 'timestamp does not match the contract pattern'],
    ['no offset', '2026-10-09T03:00:00', 'timestamp does not match the contract pattern'],
  ])('should reject %s', (_, text, problem) => {
    expect(() => parseTimestamp(text)).toThrow(rejection([problem]))
  })
})
