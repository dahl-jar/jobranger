const HTTP_PROTOCOLS = new Set(['http:', 'https:'])
const TRAILING_SLASHES = /\/+$/

export function urlOverride(override: string | undefined, committed: string): string {
  if (override === undefined || override === '') return committed
  if (!URL.canParse(override) || !HTTP_PROTOCOLS.has(new URL(override).protocol)) {
    throw new Error(`${override} is not an http(s) URL`)
  }
  return override.replace(TRAILING_SLASHES, '')
}

export const SOURCE_URL = 'https://github.com/dahl-jar/jobranger'
export const RELAY_URL = urlOverride(import.meta.env.VITE_RELAY_URL, 'https://relay.jobranger.no')
