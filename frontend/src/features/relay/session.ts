import { confirmationWords } from '@/features/pairing/confirmationWords'
import { decodeBase64Url, encodeBase64Url } from '@/features/relay/base64url'
import { concatBytes, sameBytes } from '@/features/relay/bytes'
import { CIPHERTEXT_B64_MAX_CHARS, GCM_TAG_BYTES, PAIR_TAG_BYTES, PAIR_TAG_LABEL } from '@/features/relay/contract/limits'
import { NONCE_B_BYTES, type OutgoingHello, validateAgent } from '@/features/relay/hello'

type CommitHello = Extract<OutgoingHello, { t: 'commit' }>

const LABEL = 'jobranger v1'
const COMMIT_LABEL = 'jobranger v1 commit'
const KEYS_INFO = 'jobranger v1 keys'
const SAS_INFO = 'jobranger v1 sas'
const UTF8 = new TextEncoder()
const SID_BYTES = 16
const SHARED_SECRET_BITS = 256
const OKM_BITS = 768
const SAS_BITS = 64
const KEY_BYTES = 32
const TAG_BITS = GCM_TAG_BYTES * 8
const NONCE_PREFIX_BYTES = 4
const SEQ_BYTES = 8
const SEQ_LIMIT = 2 ** 32
const CLI_TO_BROWSER = 0x01
const BROWSER_TO_CLI = 0x02

type HandshakeFailure = 'cli_key' | 'mac' | 'zero'
type FrameFailure = 'seq' | 'size' | 'tag' | 'closed'

export class HandshakeError extends Error {
  readonly reason: HandshakeFailure

  constructor(reason: HandshakeFailure) {
    super(`handshake failed: ${reason}`)
    this.name = 'HandshakeError'
    this.reason = reason
  }
}

export class FrameError extends Error {
  readonly reason: FrameFailure

  constructor(reason: FrameFailure) {
    super(`frame rejected: ${reason}`)
    this.name = 'FrameError'
    this.reason = reason
  }
}

export interface HandshakeSecrets {
  ephemeral: CryptoKeyPair
  sid: Uint8Array
  nonceB: Uint8Array
}

export interface PendingHandshake {
  readonly sid: string
  readonly sidBytes: Uint8Array
  readonly webStatic: CryptoKeyPair
  readonly browserStatic: Uint8Array
  readonly ephemeral: CryptoKeyPair
  readonly browserEph: Uint8Array
  readonly nonceB: Uint8Array
  readonly agent: string
  readonly agentPart: Uint8Array
}

export interface OfferedHandshake {
  readonly sid: string
  readonly sidBytes: Uint8Array
  readonly cliStatic: Uint8Array
  readonly th: Uint8Array
  readonly confirmKey: CryptoKey
  readonly sendKey: CryptoKey
  readonly receiveKey: CryptoKey
  readonly words: string[]
}

function u16be(value: number): Uint8Array {
  const bytes = new Uint8Array(2)
  new DataView(bytes.buffer).setUint16(0, value)
  return bytes
}

function u64be(value: number): Uint8Array {
  const bytes = new Uint8Array(SEQ_BYTES)
  new DataView(bytes.buffer).setBigUint64(0, BigInt(value))
  return bytes
}

async function sha256(...parts: Uint8Array[]): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest('SHA-256', concatBytes(...parts)))
}

async function rawPublicKey(pair: CryptoKeyPair): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.exportKey('raw', pair.publicKey))
}

async function sharedSecret(privateKey: CryptoKey, peerPublic: Uint8Array): Promise<Uint8Array> {
  let secret: Uint8Array
  try {
    const publicKey = await crypto.subtle.importKey('raw', concatBytes(peerPublic), { name: 'X25519' }, true, [])
    secret = new Uint8Array(await crypto.subtle.deriveBits({ name: 'X25519', public: publicKey }, privateKey, SHARED_SECRET_BITS))
  } catch {
    throw new HandshakeError('zero')
  }
  if (secret.every((byte) => byte === 0)) throw new HandshakeError('zero')
  return secret
}

async function hkdf(ikm: Uint8Array, salt: Uint8Array, info: string, bits: number): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey('raw', concatBytes(ikm), 'HKDF', false, ['deriveBits'])
  const params = { name: 'HKDF', hash: 'SHA-256', salt: concatBytes(salt), info: UTF8.encode(info) }
  return new Uint8Array(await crypto.subtle.deriveBits(params, key, bits))
}

function importAesKey(raw: Uint8Array, usage: 'encrypt' | 'decrypt'): Promise<CryptoKey> {
  return crypto.subtle.importKey('raw', concatBytes(raw), 'AES-GCM', false, [usage])
}

function frameParams(sid: Uint8Array, direction: number, seq: number): AesGcmParams {
  return {
    name: 'AES-GCM',
    iv: concatBytes(new Uint8Array(NONCE_PREFIX_BYTES), u64be(seq)),
    additionalData: concatBytes(UTF8.encode(LABEL), sid, new Uint8Array([direction]), u64be(seq)),
    tagLength: TAG_BITS,
  }
}

export async function createHandshakeSecrets(): Promise<HandshakeSecrets> {
  return {
    ephemeral: await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits']),
    sid: crypto.getRandomValues(new Uint8Array(SID_BYTES)),
    nonceB: crypto.getRandomValues(new Uint8Array(NONCE_B_BYTES)),
  }
}

export async function pairTag(code: string, channel: string): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(code), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign'])
  const mac = await crypto.subtle.sign('HMAC', key, concatBytes(UTF8.encode(PAIR_TAG_LABEL), decodeBase64Url(channel)))
  return new Uint8Array(mac).slice(0, PAIR_TAG_BYTES)
}

export async function beginHandshake(input: { webStatic: CryptoKeyPair; agent: string; secrets?: HandshakeSecrets; pairTag?: Uint8Array }) {
  const agentBytes = validateAgent(input.agent)
  const secrets = input.secrets ?? (await createHandshakeSecrets())
  const browserStatic = await rawPublicKey(input.webStatic)
  const browserEph = await rawPublicKey(secrets.ephemeral)
  const agentPart = concatBytes(u16be(agentBytes.length), agentBytes)
  const commit = await sha256(UTF8.encode(COMMIT_LABEL), browserStatic, browserEph, secrets.nonceB, agentPart)
  const pending: PendingHandshake = {
    sid: encodeBase64Url(secrets.sid),
    sidBytes: secrets.sid,
    webStatic: input.webStatic,
    browserStatic,
    ephemeral: secrets.ephemeral,
    browserEph,
    nonceB: secrets.nonceB,
    agent: input.agent,
    agentPart,
  }
  const fields: CommitHello = { t: 'commit', browserStatic, commit }
  return { commit: input.pairTag === undefined ? fields : { ...fields, pairTag: input.pairTag }, pending }
}

export async function answerOffer(
  pending: PendingHandshake,
  offer: { t: 'offer'; cliStatic: Uint8Array; cliEph: Uint8Array },
  knownCliStatic: Uint8Array | undefined,
) {
  if (knownCliStatic !== undefined && !sameBytes(knownCliStatic, offer.cliStatic)) throw new HandshakeError('cli_key')
  const ikm = concatBytes(
    await sharedSecret(pending.ephemeral.privateKey, offer.cliEph),
    await sharedSecret(pending.ephemeral.privateKey, offer.cliStatic),
    await sharedSecret(pending.webStatic.privateKey, offer.cliEph),
  )
  const th = await sha256(
    UTF8.encode(LABEL),
    pending.sidBytes,
    offer.cliStatic,
    pending.browserStatic,
    offer.cliEph,
    pending.browserEph,
    pending.nonceB,
    pending.agentPart,
  )
  const okm = await hkdf(ikm, th, KEYS_INFO, OKM_BITS)
  const offered: OfferedHandshake = {
    sid: pending.sid,
    sidBytes: pending.sidBytes,
    cliStatic: offer.cliStatic,
    th,
    receiveKey: await importAesKey(okm.slice(0, KEY_BYTES), 'decrypt'),
    sendKey: await importAesKey(okm.slice(KEY_BYTES, 2 * KEY_BYTES), 'encrypt'),
    confirmKey: await crypto.subtle.importKey('raw', okm.slice(2 * KEY_BYTES), { name: 'HMAC', hash: 'SHA-256' }, false, [
      'sign',
      'verify',
    ]),
    words: confirmationWords(await hkdf(ikm, th, SAS_INFO, SAS_BITS)),
  }
  const reveal = { t: 'reveal', browserEph: pending.browserEph, nonceB: pending.nonceB, agent: pending.agent } as const
  return { reveal, offered }
}

export async function finishHandshake(offered: OfferedHandshake, cliConfirm: { t: 'confirm'; mac: Uint8Array }) {
  const cliMessage = concatBytes(UTF8.encode('cli'), offered.th)
  if (!(await crypto.subtle.verify('HMAC', offered.confirmKey, concatBytes(cliConfirm.mac), cliMessage))) {
    throw new HandshakeError('mac')
  }
  const browserMac = await crypto.subtle.sign('HMAC', offered.confirmKey, concatBytes(UTF8.encode('browser'), offered.th))
  return {
    session: new Session(offered),
    confirm: { t: 'confirm', mac: new Uint8Array(browserMac) } as const,
    words: offered.words,
  }
}

export class Session {
  readonly cliStatic: Uint8Array
  readonly #sidBytes: Uint8Array
  readonly #sendKey: CryptoKey
  readonly #receiveKey: CryptoKey
  #sentSeq = 0
  #receivedSeq = 0
  #closed = false

  constructor(offered: OfferedHandshake) {
    this.cliStatic = offered.cliStatic
    this.#sidBytes = offered.sidBytes
    this.#sendKey = offered.sendKey
    this.#receiveKey = offered.receiveKey
  }

  #reject(reason: FrameFailure): never {
    this.#closed = true
    throw new FrameError(reason)
  }

  async seal(plaintext: Uint8Array): Promise<{ seq: number; ciphertext: string }> {
    if (this.#closed) throw new FrameError('closed')
    const seq = this.#sentSeq + 1
    if (seq >= SEQ_LIMIT) this.#reject('seq')
    this.#sentSeq = seq
    const sealed = await crypto.subtle.encrypt(frameParams(this.#sidBytes, BROWSER_TO_CLI, seq), this.#sendKey, concatBytes(plaintext))
    return { seq, ciphertext: encodeBase64Url(new Uint8Array(sealed)) }
  }

  async open(seq: number, ciphertext: string): Promise<Uint8Array> {
    if (this.#closed) throw new FrameError('closed')
    if (seq !== this.#receivedSeq + 1 || seq >= SEQ_LIMIT) this.#reject('seq')
    if (ciphertext.length > CIPHERTEXT_B64_MAX_CHARS) this.#reject('size')
    let sealed: Uint8Array
    try {
      sealed = decodeBase64Url(ciphertext)
    } catch {
      this.#reject('size')
    }
    if (sealed.length < GCM_TAG_BYTES) this.#reject('size')
    this.#receivedSeq = seq
    try {
      return new Uint8Array(await crypto.subtle.decrypt(frameParams(this.#sidBytes, CLI_TO_BROWSER, seq), this.#receiveKey, concatBytes(sealed)))
    } catch {
      return this.#reject('tag')
    }
  }

  close(): void {
    this.#closed = true
  }
}
