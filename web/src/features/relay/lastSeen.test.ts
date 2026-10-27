import { describe, expect, it } from 'vitest'
import { clearLastSeen, readLastSeen, type SeenStorage, writeLastSeen } from '@/features/relay/lastSeen'
import { LAST_SEEN_KEY, memoryStorage, SEEN_AT } from '@/test/relayFixtures'

const BLOCKED: SeenStorage = {
  getItem: () => {
    throw new Error('storage blocked')
  },
  setItem: () => {
    throw new Error('storage blocked')
  },
  removeItem: () => {
    throw new Error('storage blocked')
  },
}

describe('lastSeen', () => {
  it('should read back the time it wrote under jobranger.lastSeen', () => {
    const storage = memoryStorage()

    writeLastSeen(SEEN_AT, storage)

    expect([readLastSeen(storage), Object.keys(storage.entries)]).toEqual([SEEN_AT, [LAST_SEEN_KEY]])
  })

  it('should read nothing when the stored value is not a time', () => {
    expect(readLastSeen(memoryStorage({ [LAST_SEEN_KEY]: 'yesterday' }))).toBeUndefined()
  })

  it('should read nothing when storage is blocked', () => {
    expect(readLastSeen(BLOCKED)).toBeUndefined()
  })

  it('should not throw when storage refuses the write', () => {
    expect(() => writeLastSeen(SEEN_AT, BLOCKED)).not.toThrow()
  })

  it('should read nothing after the time is cleared', () => {
    const storage = memoryStorage()
    writeLastSeen(SEEN_AT, storage)

    clearLastSeen(storage)

    expect(readLastSeen(storage)).toBeUndefined()
  })

  it('should not throw when storage refuses the clear', () => {
    expect(() => clearLastSeen(BLOCKED)).not.toThrow()
  })
})
