import { createContext, useContext } from 'react'
import type { ThemeChoice } from '@/app/theme/theme'

interface ThemeContextValue {
  choice: ThemeChoice
  setChoice: (choice: ThemeChoice) => void
}

export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined)

export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext)
  if (value === undefined) throw new Error('useTheme needs a ThemeProvider above it')
  return value
}
