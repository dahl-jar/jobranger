import { createContext, useContext } from 'react'
import { APP_BASE } from '@/helpers/appPaths'

export const AppBaseContext = createContext(APP_BASE)

export function useAppBase(): string {
  return useContext(AppBaseContext)
}
