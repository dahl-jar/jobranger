import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { en, type Messages } from '@/shared/i18n/en'
import { initialLanguage, LANGUAGE_STORAGE_KEY, saveLanguage, type Language } from '@/shared/i18n/language'
import { LanguageContext } from '@/shared/i18n/languageContext'
import { nb } from '@/shared/i18n/nb'

const MESSAGES: Record<Language, Messages> = { en, nb }

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setCurrent] = useState(initialLanguage)

  useEffect(() => {
    function followOtherTab(event: StorageEvent) {
      if (event.key === LANGUAGE_STORAGE_KEY || event.key === null) setCurrent(initialLanguage())
    }
    window.addEventListener('storage', followOtherTab)
    return () => window.removeEventListener('storage', followOtherTab)
  }, [])

  useEffect(() => {
    document.documentElement.lang = language
  }, [language])

  const value = useMemo(
    () => ({
      language,
      text: MESSAGES[language],
      setLanguage: (next: Language) => {
        saveLanguage(next)
        setCurrent(next)
      },
    }),
    [language],
  )

  return <LanguageContext value={value}>{children}</LanguageContext>
}
