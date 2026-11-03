const RELOAD_KEY = 'jobranger-route-reload'
const attempts = new WeakMap<object, string>()

export class RouteImportError extends Error {
  constructor(cause: unknown) {
    super('page code failed to load', { cause })
    this.name = 'RouteImportError'
  }
}

function claimReload(href: string): boolean {
  try {
    if (sessionStorage.getItem(RELOAD_KEY) === href) {
      sessionStorage.removeItem(RELOAD_KEY)
      return false
    }
    sessionStorage.setItem(RELOAD_KEY, href)
    return true
  } catch {
    return false
  }
}

function recordAttempt(failure: unknown, locationKey: string): boolean {
  if (typeof failure !== 'object' || failure === null) return true
  const isNew = attempts.get(failure) !== locationKey
  attempts.set(failure, locationKey)
  return isNew
}

export function reloadOnce(href: string, failure: unknown, locationKey: string): void {
  if (!navigator.onLine || !recordAttempt(failure, locationKey)) return
  if (claimReload(href)) window.location.assign(`${window.location.origin}${href}`)
}

function forgetReload(): void {
  try {
    sessionStorage.removeItem(RELOAD_KEY)
  } catch {}
}

export function routeImport<T>(load: () => Promise<T>): () => Promise<T> {
  return async () => {
    let loaded: T
    try {
      loaded = await load()
    } catch (error: unknown) {
      throw new RouteImportError(error)
    }
    forgetReload()
    return loaded
  }
}
