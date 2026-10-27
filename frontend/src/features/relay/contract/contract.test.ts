import { describe, expect, it } from 'vitest'
import { ENVELOPE_INVALID_COUNT, PAYLOAD_INVALID_COUNT } from '@/test/fixtureCounts'
import { envelopeFixtures, payloadFixtures } from './fixtures.gen'
import { CIPHERTEXT_B64_MAX_CHARS, GCM_TAG_BYTES, HELLO_MAX_BYTES, MAX_INTEGER_DIGITS, MAX_JSON_DEPTH } from './limits'
import { ContractError, parseEnvelope, parsePayload, parseTimestamp } from './validate'

const invalidPayloads = Object.entries(
  import.meta.glob<string>('@messages/fixtures/payload-invalid/*.json', {
    eager: true,
    query: '?raw',
    import: 'default',
  }),
)
const invalidEnvelopes = Object.entries(
  import.meta.glob<string>('@messages/fixtures/envelope-invalid/*.json', {
    eager: true,
    query: '?raw',
    import: 'default',
  }),
)
const OVERSIZE_TIMEOUT_MS = 30_000
const OVERSIZE_EMAIL = `a@${'b.'.repeat(4_000_000)}cc`
const MAX_SEQ = 2 ** 32 - 1
const HELLO_MIN_CIPHERTEXT = 'AAAA'
const base64UrlChars = (bytes: number) => Math.ceil((bytes * 4) / 3)
const adMarkText = JSON.stringify(payloadFixtures['ad.mark'])
const nested = (depth: number) => `${'['.repeat(depth)}${']'.repeat(depth)}`
const withExtra = (extra: string) => adMarkText.replace('"v":1,', `"v":1,"extra":${extra},`)
const payloadText = (name: keyof typeof payloadFixtures, from: string, to: string) =>
  JSON.stringify(payloadFixtures[name]).replace(from, to)
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
    expect(() => parsePayload(raw)).toThrow(ContractError)
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

  it.each([
    ['ads.snapshot.hidden-counts-sum-mismatch.json', '/hidden_counts must sum to hidden_total'],
    ['ads.snapshot.hidden-counts-extra-string.json', '/hidden_counts must sum to hidden_total'],
    ['ads.snapshot.hidden-counts-extra-masks-sum.json', '/hidden_counts must sum to hidden_total'],
    ['ads.snapshot.hidden-kind-under-count.json', '/hidden_counts/senior must not be below the listed hidden ads'],
  ])('should name the hidden count rule for %s', (name, problem) => {
    const raw = invalidPayloads.find(([path]) => path.endsWith(`/${name}`))?.[1]

    expect(() => parsePayload(raw ?? '')).toThrow(rejection([problem], 'ads.snapshot'))
  })

  it('should ignore an unknown hidden count key', () => {
    const snapshot = payloadFixtures['ads.snapshot']
    const raw = { ...snapshot, hidden_counts: { ...snapshot.hidden_counts, unknown: 'a' } }

    expect(parsePayload(JSON.stringify(raw))).toEqual(raw)
  })

  it('should reject an oversize email address before its pattern runs', () => {
    const snapshot = payloadFixtures['config.snapshot']
    const raw = {
      ...snapshot,
      notify: { ...snapshot.notify, email: { on: true, address: OVERSIZE_EMAIL } },
    }

    expect(() => parsePayload(JSON.stringify(raw))).toThrow(ContractError)
  }, OVERSIZE_TIMEOUT_MS)

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

  it.each([
    ['an exponent', payloadText('application.update', '"application_id":2', '"application_id":2e0'), '/application_id', 'application.update'],
    ['a fraction in a constant', payloadText('ad.mark', '"v":1', '"v":1.0'), '/v', 'ad.mark'],
    ['a fraction in a nullable field', payloadText('run.progress', '"found_new":12', '"found_new":12.0'), '/sources/0/found_new', 'run.progress'],
    ['a fraction in a later array item', payloadText('usage.snapshot', '"calls":41', '"calls":41.0'), '/days/1/calls', 'usage.snapshot'],
    ['a fraction under an escaped key', payloadText('application.update', '"application_id":2', '"\\u0061pplication_id":2.0'), '/application_id', 'application.update'],
  ])('should reject an integer written with %s', (_, text, pointer, messageType) => {
    expect(() => parsePayload(text)).toThrow(rejection([`${pointer} must be integer`], messageType))
  })

  it.each([
    ['a whole float where the schema wants a number', payloadText('usage.snapshot', '"month_usd":2.35', '"month_usd":2.0'), { month_usd: 2 }],
    ['a fraction in an unknown field', withExtra('1.0'), { extra: 1 }],
    ['a fraction in a field another message type keeps as integer', payloadText('ad.mark', '"v":1,', '"v":1,"application_id":2.0,'), { application_id: 2 }],
  ])('should accept %s', (_, text, fields) => {
    expect(parsePayload(text)).toMatchObject(fields)
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
    expect(() => parseEnvelope(raw)).toThrow(ContractError)
  })

  it('should name the kind when an envelope is rejected', () => {
    const text = JSON.stringify({ ...envelopeFixtures.data, seq: MAX_SEQ + 1 })

    expect(() => parseEnvelope(text)).toThrow(rejection([`/seq must be <= ${MAX_SEQ}`], 'data'))
  })

  it.each([
    ['first seq', { ...envelopeFixtures.data, seq: 1 }],
    ['last seq', { ...envelopeFixtures.data, seq: MAX_SEQ }],
    ['shortest hello step', { ...envelopeFixtures.hello, ciphertext: HELLO_MIN_CIPHERTEXT }],
    ['shortest data ciphertext', { ...envelopeFixtures.data, ciphertext: 'A'.repeat(base64UrlChars(GCM_TAG_BYTES)) }],
    ['longest data ciphertext', { ...envelopeFixtures.data, ciphertext: 'A'.repeat(CIPHERTEXT_B64_MAX_CHARS) }],
    ['longest hello step', { ...envelopeFixtures.hello, ciphertext: 'A'.repeat(base64UrlChars(HELLO_MAX_BYTES)) }],
  ])('should accept envelope at bound for %s', (_, envelope) => {
    expect(parseEnvelope(JSON.stringify(envelope))).toEqual(envelope)
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

describe('ContractError', () => {
  it('should list every problem in its message', () => {
    expect(new ContractError(['/seq must be integer', '/v must be equal to constant'], 'data').message).toBe(
      'contract violation: /seq must be integer; /v must be equal to constant',
    )
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
