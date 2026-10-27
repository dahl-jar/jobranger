import { createContext, useContext } from 'react'
import { DEFAULT_LIST } from '@/features/jobs/selectors'

export const APP_BASE = '/app'
export const DEMO_BASE = '/demo'

export const AppBaseContext = createContext(APP_BASE)

export function useAppBase(): string {
  return useContext(AppBaseContext)
}

export function jobsPath(base: string): string {
  return `${base}/${DEFAULT_LIST}`
}

export function settingsPath(base: string, section?: string): string {
  return section === undefined ? `${base}/settings` : `${base}/settings/${section}`
}
