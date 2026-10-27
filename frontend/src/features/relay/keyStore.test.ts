import 'fake-indexeddb/auto'
import { createStore, get } from 'idb-keyval'
import { TEST_CHANNEL } from '@/test/fakeRelay'
import { storedPairing, withoutCliKey } from '@/test/relayFixtures'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearPairing, clearPairingOf, generateWebKeys, loadPairing, probeStorage, savePairing, StorageError, type StoredPairing } from '@/features/relay/keyStore'

const engine = vi.hoisted(() => ({ dropKeys: false, failWrites: false, rewrite: undefined as ((value: Record<string, unknown>) => unknown) | undefined }))

vi.mock('idb-keyval', async (importOriginal) => {
  const actual = await importOriginal<typeof import('idb-keyval')>()
  return {
    ...actual,
    set: async (key: IDBValidKey, value: unknown, store?: Parameters<typeof actual.set>[2]) => {
      if (engine.failWrites) throw new DOMException('write blocked', 'QuotaExceededError')
      const isObject = typeof value === 'object' && value !== null
      const dropped = engine.dropKeys && isObject ? { ...value, webStatic: null } : value
      const stored = engine.rewrite && isObject ? engine.rewrite({ ...value }) : dropped
      return actual.set(key, stored, store)
    },
  }
})

const CLI_KEY = new Uint8Array(32).fill(7)

async function pairing(overrides: Partial<StoredPairing> = {}): Promise<StoredPairing> {
  return { ...withoutCliKey(await storedPairing({ pending: true })), ...overrides }
}

afterEach(async () => {
  engine.dropKeys = false
  engine.failWrites = false
  engine.rewrite = undefined
  vi.unstubAllGlobals()
  await clearPairing()
})

describe('keyStore', () => {
  it('should generate the web private key as non-extractable', async () => {
    const keys = await generateWebKeys()

    expect([keys.privateKey.extractable, keys.privateKey.usages]).toEqual([false, ['deriveBits']])
  })

  it('should load a saved pairing with a private key that still derives', async () => {
    const peer = await generateWebKeys()
    await savePairing(await pairing({ pending: false, cliStaticPublicKey: CLI_KEY }))

    const loaded = await loadPairing()

    const privateKey = loaded?.webStatic.privateKey ?? peer.publicKey
    const bits = await crypto.subtle.deriveBits({ name: 'X25519', public: peer.publicKey }, privateKey, 256)
    expect([loaded?.channel, loaded?.cliStaticPublicKey, bits.byteLength]).toEqual([TEST_CHANNEL, CLI_KEY, 32])
  })

  it('should load nothing after the pairing is cleared', async () => {
    await savePairing(await pairing())

    await clearPairing()

    expect(await loadPairing()).toBeUndefined()
  })

  it('should finish saving while the persistent storage prompt is still open', async () => {
    vi.stubGlobal('navigator', { storage: { persist: () => new Promise<boolean>(() => undefined) } })

    await savePairing(await pairing())

    expect((await loadPairing())?.ticket).toBe('ticket-one')
  })

  it('should finish saving when the persistent storage request fails', async () => {
    vi.stubGlobal('navigator', {
      storage: {
        persist: () => {
          throw new DOMException('denied', 'SecurityError')
        },
      },
    })

    await savePairing(await pairing())

    expect((await loadPairing())?.ticket).toBe('ticket-one')
  })

  it.each([
    { change: 'a different CLI key', rewrite: (value: Record<string, unknown>) => ({ ...value, cliStaticPublicKey: new Uint8Array(32) }) },
    { change: 'a shorter CLI key', rewrite: (value: Record<string, unknown>) => ({ ...value, cliStaticPublicKey: CLI_KEY.slice(1) }) },
    { change: 'a lost CLI key', rewrite: (value: Record<string, unknown>) => ({ ...value, cliStaticPublicKey: undefined }) },
    { change: 'a flipped pending flag', rewrite: (value: Record<string, unknown>) => ({ ...value, pending: true }) },
    { change: 'another channel', rewrite: (value: Record<string, unknown>) => ({ ...value, channel: 'channel-two' }) },
  ])('should throw StorageError when the pairing reads back with $change', async ({ rewrite }) => {
    engine.rewrite = rewrite

    await expect(savePairing(await pairing({ pending: false, cliStaticPublicKey: CLI_KEY }))).rejects.toThrow(StorageError)
  })

  it('should throw StorageError when a CLI key appears on read back', async () => {
    engine.rewrite = (value) => ({ ...value, cliStaticPublicKey: CLI_KEY })

    await expect(savePairing(await pairing())).rejects.toThrow(StorageError)
  })

  it('should throw StorageError when the keys do not read back', async () => {
    engine.dropKeys = true

    await expect(savePairing(await pairing())).rejects.toThrow(StorageError)
  })

  it('should ask for persistent storage after saving', async () => {
    const persist = vi.fn(async () => true)
    vi.stubGlobal('navigator', { storage: { persist } })

    await savePairing(await pairing())

    vi.unstubAllGlobals()
    expect(persist).toHaveBeenCalledOnce()
  })

  it('should save when the browser has no storage manager', async () => {
    vi.stubGlobal('navigator', {})

    await savePairing(await pairing())

    vi.unstubAllGlobals()
    expect((await loadPairing())?.ticket).toBe('ticket-one')
  })

  it.each([
    { name: 'a missing web key', value: async () => ({ ...(await pairing()), webStatic: undefined }) },
    { name: 'a web key pair of plain objects', value: async () => ({ ...(await pairing()), webStatic: { privateKey: {}, publicKey: {} } }) },
    { name: 'a web key pair without a public key', value: async () => ({ ...(await pairing()), webStatic: { privateKey: (await generateWebKeys()).privateKey } }) },
    { name: 'a numeric ticket', value: async () => ({ ...(await pairing()), ticket: 1 }) },
    { name: 'a missing paired time', value: async () => ({ ...(await pairing()), pairedAt: undefined }) },
    { name: 'a pending flag that is a string', value: async () => ({ ...(await pairing()), pending: 'false' }) },
    { name: 'a CLI key that is an array', value: async () => ({ ...(await pairing()), cliStaticPublicKey: [1, 2] }) },
    { name: 'a string', value: async () => 'paired' },
    { name: 'null', value: async () => null },
  ])('should load nothing for a stored value with $name', async ({ value }) => {
    const { set, createStore } = await import('idb-keyval')
    await set('current', await value(), createStore('jobranger', 'pairing'))

    expect(await loadPairing()).toBeUndefined()
  })
})

describe('probeStorage', () => {
  it('should report usable storage', async () => {
    expect(await probeStorage()).toBe(true)
  })

  it('should report storage that drops keys as unusable', async () => {
    engine.dropKeys = true

    expect(await probeStorage()).toBe(false)
  })

  it('should report storage that refuses writes as unusable', async () => {
    engine.failWrites = true

    expect(await probeStorage()).toBe(false)
  })

  it('should leave no probe record behind', async () => {
    await probeStorage()

    const { get, createStore } = await import('idb-keyval')
    expect(await get('probe', createStore('jobranger', 'pairing'))).toBeUndefined()
  })

  it('should keep the pairing under the current key of the jobranger pairing store', async () => {
    const saved = await pairing()

    await savePairing(saved)

    expect(await get<StoredPairing>('current', createStore('jobranger', 'pairing'))).toMatchObject({ channel: saved.channel })
  })
})

describe('clearPairingOf', () => {
  it('should clear a pairing of the given channel', async () => {
    await savePairing(await pairing())

    await clearPairingOf(TEST_CHANNEL)

    expect(await loadPairing()).toBeUndefined()
  })

  it('should keep a pairing of another channel', async () => {
    await savePairing(await pairing({ channel: 'channel-two' }))

    await clearPairingOf(TEST_CHANNEL)

    expect((await loadPairing())?.channel).toBe('channel-two')
  })

  it('should finish when nothing is stored', async () => {
    await expect(clearPairingOf(TEST_CHANNEL)).resolves.toBeUndefined()
  })
})
