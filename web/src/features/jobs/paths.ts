import { APP_BASE } from '@/app/paths'
import type { DeepLink } from '@/features/jobs/deepLink'
import type { JobList } from '@/features/jobs/selectors'

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
