import { createStore, del, get, set, type UseStore } from 'idb-keyval'
import { sameBytes } from '@/features/relay/bytes'
import { asRecord } from '@/shared/asRecord'

const DATABASE = 'jobranger'
const STORE = 'pairing'
const PAIRING_KEY = 'current'
const PROBE_KEY = 'probe'
const STRING_FIELDS = ['channel', 'ticket', 'ticketExpiresAt', 'pairedAt'] as const
const BYTES_TAG = '[object Uint8Array]'
const KEY_TAG = '[object CryptoKey]'

export interface StoredPairing {
  channel: string
  ticket: string
  ticketExpiresAt: string
  pending: boolean
  cliStaticPublicKey?: Uint8Array
  webStatic: CryptoKeyPair
  pairedAt: string
}

let store: UseStore | undefined

function pairingStore(): UseStore {
  store ??= createStore(DATABASE, STORE)
  return store
}

function hasTag(value: unknown, tag: string): boolean {
  return Object.prototype.toString.call(value) === tag
}

function isKeyPair(value: unknown): value is CryptoKeyPair {
  const fields = asRecord(value)
  return fields !== undefined && hasTag(fields.privateKey, KEY_TAG) && hasTag(fields.publicKey, KEY_TAG)
}

function isStoredPairing(value: unknown): value is StoredPairing {
  const fields = asRecord(value)
  if (fields === undefined) return false
  return (
    STRING_FIELDS.every((field) => typeof fields[field] === 'string') &&
    typeof fields.pending === 'boolean' &&
    (fields.cliStaticPublicKey === undefined || hasTag(fields.cliStaticPublicKey, BYTES_TAG)) &&
    isKeyPair(fields.webStatic)
  )
}

export function generateWebKeys(): Promise<CryptoKeyPair> {
  return crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits'])
}

export async function loadPairing(): Promise<StoredPairing | undefined> {
  const value: unknown = await get(PAIRING_KEY, pairingStore())
  if (!isStoredPairing(value)) return undefined
  const { cliStaticPublicKey, ...pairing } = value
  return cliStaticPublicKey === undefined ? pairing : { ...pairing, cliStaticPublicKey: Uint8Array.from(cliStaticPublicKey) }
}

export class StorageError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'StorageError'
  }
}

function sameKey(left: Uint8Array | undefined, right: Uint8Array | undefined): boolean {
  if (left === undefined || right === undefined) return left === right
  return sameBytes(left, right)
}

function matches(stored: unknown, pairing: StoredPairing): boolean {
  return (
    isStoredPairing(stored) &&
    stored.channel === pairing.channel &&
    stored.pending === pairing.pending &&
    sameKey(stored.cliStaticPublicKey, pairing.cliStaticPublicKey)
  )
}

function requestPersistence(): void {
  try {
    navigator.storage?.persist?.().catch(() => undefined)
  } catch {
    return
  }
}

export async function savePairing(pairing: StoredPairing): Promise<void> {
  await set(PAIRING_KEY, pairing, pairingStore())
  if (!matches(await get(PAIRING_KEY, pairingStore()), pairing)) {
    throw new StorageError('saved pairing did not read back with its keys')
  }
  requestPersistence()
}

export async function probeStorage(): Promise<boolean> {
  try {
    await set(PROBE_KEY, { webStatic: await generateWebKeys() }, pairingStore())
    const stored: unknown = await get(PROBE_KEY, pairingStore())
    await del(PROBE_KEY, pairingStore())
    return isKeyPair(asRecord(stored)?.webStatic)
  } catch {
    return false
  }
}

export async function clearPairing(): Promise<void> {
  await del(PAIRING_KEY, pairingStore())
}
