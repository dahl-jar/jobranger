import { concatBytes } from '@/features/relay/bytes'
import { confirmationWords } from '@/features/pairing/confirmationWords'
import { decodeBase64Url, encodeBase64Url } from '@/features/relay/base64url'

const LABEL = 'jobranger v1'
const UTF8 = new TextEncoder()
const KEY_BITS = 256
const OKM_BITS = 768
const SAS_BITS = 64
const TAG_BITS = 128
const NONCE_PREFIX_BYTES = 4
const SEQ_BYTES = 8
const CLI_TO_BROWSER = 0x01
const BROWSER_TO_CLI = 0x02

export interface BrowserCommit {
  browserStatic: Uint8Array
  commit: Uint8Array
}

const PAIR_LABEL = 'jobranger pair v1'
const PAIR_TAG_LENGTH = 16

export async function cliPairTag(code: string, channel: Uint8Array): Promise<string> {
  const key = await crypto.subtle.importKey('raw', UTF8.encode(code), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
  const mac = new Uint8Array(await crypto.subtle.sign('HMAC', key, concatBytes(UTF8.encode(PAIR_LABEL), channel)))
  return encodeBase64Url(mac.slice(0, PAIR_TAG_LENGTH))
}

export interface BrowserReveal {
  browserEph: Uint8Array
  nonceB: Uint8Array
  agent: string
}

interface CliKeys {
  c2b: CryptoKey
  b2c: CryptoKey
  confirm: CryptoKey
  th: Uint8Array
}

function u16(value: number): Uint8Array {
  return new Uint8Array([value >> 8, value & 0xff])
}

function u64(value: number): Uint8Array {
  const bytes = new Uint8Array(SEQ_BYTES)
  new DataView(bytes.buffer).setBigUint64(0, BigInt(value))
  return bytes
}

async function sha256(data: Uint8Array<ArrayBuffer>): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest('SHA-256', data))
}

async function rawPublic(pair: CryptoKeyPair): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.exportKey('raw', pair.publicKey))
}

async function x25519(privateKey: CryptoKey, publicRaw: Uint8Array): Promise<Uint8Array> {
  const publicKey = await crypto.subtle.importKey('raw', concatBytes(publicRaw), { name: 'X25519' }, true, [])
  return new Uint8Array(await crypto.subtle.deriveBits({ name: 'X25519', public: publicKey }, privateKey, KEY_BITS))
}

async function hkdf(ikm: Uint8Array, salt: Uint8Array, info: string, bits: number): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey('raw', concatBytes(ikm), 'HKDF', false, ['deriveBits'])
  return new Uint8Array(
    await crypto.subtle.deriveBits({ name: 'HKDF', hash: 'SHA-256', salt: concatBytes(salt), info: UTF8.encode(info) }, key, bits),
  )
}

function aesKey(raw: Uint8Array): Promise<CryptoKey> {
  return crypto.subtle.importKey('raw', concatBytes(raw), 'AES-GCM', false, ['encrypt', 'decrypt'])
}

function frameParams(sid: Uint8Array, direction: number, seq: number): AesGcmParams {
  return {
    name: 'AES-GCM',
    iv: concatBytes(new Uint8Array(NONCE_PREFIX_BYTES), u64(seq)),
    additionalData: concatBytes(UTF8.encode(LABEL), sid, new Uint8Array([direction]), u64(seq)),
    tagLength: TAG_BITS,
  }
}

export class FakeCli {
  readonly staticPair: CryptoKeyPair
  readonly staticPublic: Uint8Array
  words: string[] = []
  sid = new Uint8Array()
  private ephemeral: CryptoKeyPair | undefined
  private commit: BrowserCommit | undefined
  private keys: CliKeys | undefined
  private sentSeq = 0

  private constructor(staticPair: CryptoKeyPair, staticPublic: Uint8Array) {
    this.staticPair = staticPair
    this.staticPublic = staticPublic
  }

  static async create(staticPair?: CryptoKeyPair): Promise<FakeCli> {
    const pair = staticPair ?? (await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits']))
    return new FakeCli(pair, await rawPublic(pair))
  }

  async offer(commit: BrowserCommit, ephemeral?: CryptoKeyPair): Promise<{ cliStatic: Uint8Array; cliEph: Uint8Array }> {
    this.commit = commit
    this.ephemeral = ephemeral ?? (await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits']))
    this.sentSeq = 0
    return { cliStatic: this.staticPublic, cliEph: await rawPublic(this.ephemeral) }
  }

  async reveal(reveal: BrowserReveal, sid: Uint8Array): Promise<Uint8Array> {
    if (!this.commit || !this.ephemeral) throw new Error('reveal before offer')
    const agent = UTF8.encode(reveal.agent)
    const agentPart = concatBytes(u16(agent.length), agent)
    const commit = await sha256(concatBytes(UTF8.encode('jobranger v1 commit'), this.commit.browserStatic, reveal.browserEph, reveal.nonceB, agentPart))
    if (encodeBase64Url(commit) !== encodeBase64Url(this.commit.commit)) throw new Error('commit mismatch')
    const cliEph = await rawPublic(this.ephemeral)
    const th = await sha256(
      concatBytes(UTF8.encode(LABEL), sid, this.staticPublic, this.commit.browserStatic, cliEph, reveal.browserEph, reveal.nonceB, agentPart),
    )
    const ikm = concatBytes(
      await x25519(this.ephemeral.privateKey, reveal.browserEph),
      await x25519(this.staticPair.privateKey, reveal.browserEph),
      await x25519(this.ephemeral.privateKey, this.commit.browserStatic),
    )
    const okm = await hkdf(ikm, th, 'jobranger v1 keys', OKM_BITS)
    const confirm = await crypto.subtle.importKey('raw', okm.slice(64, 96), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
    this.keys = { c2b: await aesKey(okm.slice(0, 32)), b2c: await aesKey(okm.slice(32, 64)), confirm, th }
    this.sid = concatBytes(sid)
    this.words = confirmationWords(await hkdf(ikm, th, 'jobranger v1 sas', SAS_BITS))
    return this.mac('cli')
  }

  async mac(role: 'cli' | 'browser'): Promise<Uint8Array> {
    if (!this.keys) throw new Error('mac before reveal')
    return new Uint8Array(await crypto.subtle.sign('HMAC', this.keys.confirm, concatBytes(UTF8.encode(role), this.keys.th)))
  }

  async seal(plaintext: string | Uint8Array, seq = this.sentSeq + 1, sid = this.sid): Promise<{ seq: number; ciphertext: string }> {
    if (!this.keys) throw new Error('seal before reveal')
    this.sentSeq = seq
    const bytes = typeof plaintext === 'string' ? UTF8.encode(plaintext) : concatBytes(plaintext)
    const sealed = await crypto.subtle.encrypt(frameParams(sid, CLI_TO_BROWSER, seq), this.keys.c2b, bytes)
    return { seq, ciphertext: encodeBase64Url(new Uint8Array(sealed)) }
  }

  async open(seq: number, ciphertext: string): Promise<string> {
    if (!this.keys) throw new Error('open before reveal')
    const plain = await crypto.subtle.decrypt(frameParams(this.sid, BROWSER_TO_CLI, seq), this.keys.b2c, concatBytes(decodeBase64Url(ciphertext)))
    return new TextDecoder().decode(plain)
  }
}
