import type { DeepLink } from '@/features/jobs/deepLink'
import type { JobList } from '@/features/jobs/selectors'

export const AD_PATH = '/app/ad'

export function detailPath(adId: string): string {
  return `${AD_PATH}?id=${encodeURIComponent(adId)}`
}

export function deepLinkPath(link: DeepLink): string {
  return `${detailPath(link.adId)}&action=${link.action}`
}

export function listPath(list: JobList): string {
  return `/app/${list}`
}
