import { APP_BASE } from '@/helpers/appPaths'
import type { JobList } from '@/helpers/adSelectors'

export type DeepLinkAction = 'applied' | 'not_for_me'

export interface DeepLink {
  adId: string
  action: DeepLinkAction
}

export const AD_PATH = `${APP_BASE}/ad`

export function detailPath(adId: string, base = APP_BASE): string {
  return `${base}/ad?id=${encodeURIComponent(adId)}`
}

export function deepLinkPath(link: DeepLink): string {
  return `${detailPath(link.adId)}&action=${link.action}`
}

export function listPath(list: JobList, base: string): string {
  return `${base}/${list}`
}
