const LAST_SEEN_KEY = 'jobranger.lastSeen'

export type SeenStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>

export function readLastSeen(storage?: SeenStorage): string | undefined {
  try {
    const value = (storage ?? localStorage).getItem(LAST_SEEN_KEY)
    return value !== null && !Number.isNaN(Date.parse(value)) ? value : undefined
  } catch {
    return undefined
  }
}

export function writeLastSeen(iso: string, storage?: SeenStorage): void {
  try {
    ;(storage ?? localStorage).setItem(LAST_SEEN_KEY, iso)
  } catch {
    return
  }
}

export function clearLastSeen(storage?: SeenStorage): void {
  try {
    ;(storage ?? localStorage).removeItem(LAST_SEEN_KEY)
  } catch {
    return
  }
}
