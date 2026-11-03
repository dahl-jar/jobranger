import { DEFAULT_LIST } from '@/helpers/adSelectors'

export const APP_BASE = '/app'
export const DEMO_BASE = '/demo'

export function jobsPath(base: string): string {
  return `${base}/${DEFAULT_LIST}`
}

export function settingsPath(base: string, section?: string): string {
  return section === undefined ? `${base}/settings` : `${base}/settings/${section}`
}
