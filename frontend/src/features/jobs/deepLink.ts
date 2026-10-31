import { AD_PATH, deepLinkPath } from '@/features/jobs/paths'
import type { AdsSnapshot } from '@/features/relay/contract/payload.gen'

type DeepLinkAction = 'applied' | 'not_for_me'
type DeepLinkOutcome = 'mark' | 'marked' | 'missing'

export interface DeepLink {
  adId: string
  action: DeepLinkAction
}

interface AdLink {
  adId: string
  action: DeepLinkAction | undefined
}

const AD_ID_MAX_CHARS = 200
const CONTROL_CHARACTER = /\p{Cc}/u
const PENDING_LINK_KEY = 'jobranger.pendingLink'
const ID_PARAM = 'id'
const ACTION_PARAM = 'action'
const QUERY_START = '?'

function isAction(value: string | undefined): value is DeepLinkAction {
  return value === 'applied' || value === 'not_for_me'
}

function isAdId(value: string): boolean {
  const length = [...value].length
  return length >= 1 && length <= AD_ID_MAX_CHARS && !CONTROL_CHARACTER.test(value)
}

function single(params: URLSearchParams, name: string): string | undefined {
  const values = params.getAll(name)
  return values.length === 1 ? values[0] : undefined
}

export function parseAdSearch(search: string): AdLink | undefined {
  const params = new URLSearchParams(search)
  const adId = single(params, ID_PARAM)
  if (adId === undefined || !isAdId(adId)) return undefined
  const action = single(params, ACTION_PARAM)
  return { adId, action: isAction(action) ? action : undefined }
}

export function parseDeepLink(pathname: string, search: string): DeepLink | undefined {
  if (pathname !== AD_PATH) return undefined
  const link = parseAdSearch(search)
  if (link?.action === undefined) return undefined
  return { adId: link.adId, action: link.action }
}

function parsePath(path: string): DeepLink | undefined {
  const queryAt = path.indexOf(QUERY_START)
  if (queryAt < 0 || path.includes('#')) return undefined
  return parseDeepLink(path.slice(0, queryAt), path.slice(queryAt))
}

export function savePendingDeepLink(path: string): void {
  try {
    sessionStorage.setItem(PENDING_LINK_KEY, path)
  } catch {
    return
  }
}

export function takePendingDeepLink(): string | undefined {
  try {
    const path = sessionStorage.getItem(PENDING_LINK_KEY)
    sessionStorage.removeItem(PENDING_LINK_KEY)
    const link = path === null ? undefined : parsePath(path)
    return link === undefined ? undefined : deepLinkPath(link)
  } catch {
    return undefined
  }
}

export function deepLinkOutcome(snapshot: AdsSnapshot, link: DeepLink): DeepLinkOutcome {
  const ad = snapshot.active.find((candidate) => candidate.id === link.adId)
  if (ad === undefined) return 'missing'
  return ad.mark === link.action ? 'marked' : 'mark'
}
