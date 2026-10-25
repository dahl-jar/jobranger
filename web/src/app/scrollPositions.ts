import type { Location } from 'react-router'

const SCROLL_POSITIONS_KEY = 'react-router-scroll-positions'

const FIRST_ENTRY_KEY = 'default'
const RESTORING_LOADS: ReadonlySet<string | undefined> = new Set(['reload', 'back_forward'])

type PageUrl = Pick<Location, 'pathname' | 'search' | 'hash'>

function urlKey(url: PageUrl): string {
  return `${url.pathname}${url.search}${url.hash}`
}

export function scrollKey(location: Location): string {
  return location.key === FIRST_ENTRY_KEY ? urlKey(location) : location.key
}

export function forgetScroll(url: PageUrl, navigationType: string | undefined): void {
  if (RESTORING_LOADS.has(navigationType)) return
  try {
    const saved: unknown = JSON.parse(sessionStorage.getItem(SCROLL_POSITIONS_KEY) ?? '{}')
    if (!(saved instanceof Object)) return
    const kept = Object.fromEntries(Object.entries(saved).filter(([key]) => key !== urlKey(url)))
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, JSON.stringify(kept))
  } catch {}
}
