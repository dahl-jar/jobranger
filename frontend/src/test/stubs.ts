import { onTestFinished, vi } from 'vitest'

export const IMPORT_FAILURE = 'Failed to fetch dynamically imported module'

export function stubLocation() {
  const assign = vi.fn()
  vi.stubGlobal('location', { assign, reload: vi.fn(), origin: window.location.origin })
  onTestFinished(() => {
    vi.unstubAllGlobals()
  })
  return assign
}

export function blockStorage(method: 'setItem' | 'removeItem') {
  return vi.spyOn(Storage.prototype, method).mockImplementation(() => {
    throw new DOMException('The operation is insecure.', 'SecurityError')
  })
}
