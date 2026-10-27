import { decodeBase64Url } from '@/features/relay/base64url'

export interface SessionVector {
  cli_static_public: string
  cli_eph_public: string
  browser_static_public: string
  browser_eph_public: string
  sid: string
  nonce_b: string
  agent: string
  commit: string
  transcript_hash: string
  ikm: string
  key_c2b: string
  key_b2c: string
  key_confirm: string
  sas: string
  words: string[]
  confirm_cli: string
  confirm_browser: string
  frame_seq: number
  frame_plaintext: string
  frame_ciphertext: string
  channel: string
  pair_code: string
  pair_tag: string
}

const PKCS8_X25519_PREFIX = new Uint8Array([
  0x30, 0x2e, 0x02, 0x01, 0x00, 0x30, 0x05, 0x06, 0x03, 0x2b, 0x65, 0x6e, 0x04, 0x22, 0x04, 0x20,
])
const SID_BYTES = 16

const files = import.meta.glob<SessionVector>('@contracts/fixtures/crypto/session-v1.json', {
  eager: true,
  import: 'default',
})

const loaded = Object.values(files)[0]
if (loaded === undefined) throw new Error('contracts/fixtures/crypto/session-v1.json is missing')

export const SESSION_VECTOR: SessionVector = loaded

export const VECTOR_LABELS = {
  cliStatic: 'jobranger vector cli static',
  cliEph: 'jobranger vector cli ephemeral',
  browserStatic: 'jobranger vector browser static',
  browserEph: 'jobranger vector browser ephemeral',
  sid: 'jobranger vector sid',
  nonce: 'jobranger vector nonce',
} as const

export async function labelHash(label: string): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(label)))
}

export async function labelBytes16(label: string): Promise<Uint8Array> {
  return (await labelHash(label)).slice(0, SID_BYTES)
}

export async function vectorKeyPair(label: string, publicKey: string): Promise<CryptoKeyPair> {
  const pkcs8 = new Uint8Array([...PKCS8_X25519_PREFIX, ...(await labelHash(label))])
  const privateKey = await crypto.subtle.importKey('pkcs8', pkcs8, { name: 'X25519' }, false, ['deriveBits'])
  const publicCryptoKey = await crypto.subtle.importKey('raw', decodeBase64Url(publicKey), { name: 'X25519' }, true, [])
  return { privateKey, publicKey: publicCryptoKey }
}
