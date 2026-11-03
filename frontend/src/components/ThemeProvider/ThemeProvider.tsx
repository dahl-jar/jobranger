import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { paintTheme, readStoredTheme, saveTheme, watchStoredTheme, watchSystemTheme, type ThemeChoice } from '@/helpers/applyTheme'
import { ThemeContext } from '@/hooks/useTheme'

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [choice, setCurrent] = useState(readStoredTheme)

  useEffect(() => watchStoredTheme(() => setCurrent(readStoredTheme())), [])

  useEffect(() => {
    paintTheme(choice)
    if (choice !== 'system') return undefined
    return watchSystemTheme(() => paintTheme('system'))
  }, [choice])

  const value = useMemo(
    () => ({
      choice,
      setChoice: (next: ThemeChoice) => {
        saveTheme(next)
        setCurrent(next)
      },
    }),
    [choice],
  )

  return <ThemeContext value={value}>{children}</ThemeContext>
}
