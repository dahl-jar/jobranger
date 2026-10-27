import { decodeBase64Url, encodeBase64Url } from '@/features/relay/base64url'
import { AGENT_MAX_BYTES, HELLO_MAX_BYTES } from '@/features/relay/contract/limits'
import { scanJson } from '@/features/relay/contract/scan'
import { asRecord } from '@/shared/asRecord'

const KEY_BYTES = 32
const MAC_BYTES = 32
export const NONCE_B_BYTES = 16

export type IncomingHello =
  | { t: 'offer'; cliStatic: Uint8Array; cliEph: Uint8Array }
  | { t: 'confirm'; mac: Uint8Array }
  | { t: 'unknown_device' }
  | { t: 'abort' }
  | { t: 'unknown_session' }

export type OutgoingHello =
  | { t: 'commit'; browserStatic: Uint8Array; commit: Uint8Array; pairTag?: Uint8Array }
  | { t: 'reveal'; browserEph: Uint8Array; nonceB: Uint8Array; agent: string }
  | { t: 'confirm'; mac: Uint8Array }
  | { t: 'abort' }

const INCOMING_FIELDS: Record<IncomingHello['t'], Record<string, number>> = {
  offer: { cli_static: KEY_BYTES, cli_eph: KEY_BYTES },
  confirm: { mac: MAC_BYTES },
  unknown_device: {},
  abort: {},
  unknown_session: {},
}

const LONE_SURROGATE = /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/

export class HelloError extends Error {
  constructor(message: string) {
    super(`hello step rejected: ${message}`)
    this.name = 'HelloError'
  }
}

function isIncomingStep(step: unknown): step is IncomingHello['t'] {
  return typeof step === 'string' && Object.hasOwn(INCOMING_FIELDS, step)
}

function decodeText(ciphertext: string): string {
  let bytes: Uint8Array
  try {
    bytes = decodeBase64Url(ciphertext)
  } catch {
    throw new HelloError('ciphertext is not canonical base64url')
  }
  if (bytes.length > HELLO_MAX_BYTES) throw new HelloError(`step over ${HELLO_MAX_BYTES} bytes`)
  try {
    return new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes)
  } catch {
    throw new HelloError('step is not UTF-8')
  }
}

function parseObject(text: string): Record<string, unknown> {
  const { problem } = scanJson(text)
  if (problem) throw new HelloError(problem)
  let value: unknown
  try {
    value = JSON.parse(text)
  } catch {
    throw new HelloError('step is not JSON')
  }
  const fields = asRecord(value)
  if (fields === undefined || Array.isArray(value)) throw new HelloError('step is not a JSON object')
  return fields
}

function readBinary(value: unknown, size: number, name: string): Uint8Array {
  if (typeof value !== 'string') throw new HelloError(`${name} is not a string`)
  let bytes: Uint8Array
  try {
    bytes = decodeBase64Url(value)
  } catch {
    throw new HelloError(`${name} is not canonical base64url`)
  }
  if (bytes.length !== size) throw new HelloError(`${name} has ${bytes.length} bytes, expected ${size}`)
  return bytes
}

export function parseHello(ciphertext: string): IncomingHello {
  const fields = parseObject(decodeText(ciphertext))
  const step = fields.t
  if (!isIncomingStep(step)) throw new HelloError('unknown step')
  const sizes = INCOMING_FIELDS[step]
  const expected = ['t', ...Object.keys(sizes)]
  const keys = Object.keys(fields)
  if (keys.length !== expected.length || !expected.every((key) => keys.includes(key))) {
    throw new HelloError(`${step} needs exactly the fields ${expected.join(', ')}`)
  }
  const read = (name: string): Uint8Array => readBinary(fields[name], sizes[name] ?? 0, name)
  switch (step) {
    case 'offer':
      return { t: step, cliStatic: read('cli_static'), cliEph: read('cli_eph') }
    case 'confirm':
      return { t: step, mac: read('mac') }
    default:
      return { t: step }
  }
}

function wireFields(hello: OutgoingHello): Record<string, string> {
  switch (hello.t) {
    case 'commit': {
      const fields = { t: hello.t, browser_static: encodeBase64Url(hello.browserStatic), commit: encodeBase64Url(hello.commit) }
      return hello.pairTag === undefined ? fields : { ...fields, pair_tag: encodeBase64Url(hello.pairTag) }
    }
    case 'reveal':
      return { t: hello.t, browser_eph: encodeBase64Url(hello.browserEph), nonce_b: encodeBase64Url(hello.nonceB), agent: hello.agent }
    case 'confirm':
      return { t: hello.t, mac: encodeBase64Url(hello.mac) }
    case 'abort':
      return { t: hello.t }
  }
}

export function encodeHello(hello: OutgoingHello): string {
  return encodeBase64Url(new TextEncoder().encode(JSON.stringify(wireFields(hello))))
}

export function validateAgent(agent: string): Uint8Array {
  if (LONE_SURROGATE.test(agent)) throw new HelloError('agent holds a lone surrogate')
  const bytes = new TextEncoder().encode(agent)
  if (bytes.length > AGENT_MAX_BYTES) throw new HelloError(`agent over ${AGENT_MAX_BYTES} bytes`)
  return bytes
}
