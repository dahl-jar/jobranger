import { createContext, useContext } from 'react'
import { en, type Messages } from '@/shared/i18n/en'
import type { Language } from '@/shared/i18n/language'

interface LanguageContextValue {
  language: Language
  text: Messages
  setLanguage: (language: Language) => void
}

const ENGLISH_WITHOUT_PROVIDER: LanguageContextValue = {
  language: 'en',
  text: en,
  setLanguage: () => undefined,
}

export const LanguageContext = createContext<LanguageContextValue>(ENGLISH_WITHOUT_PROVIDER)

export function useLanguage(): Pick<LanguageContextValue, 'language' | 'setLanguage'> {
  const { language, setLanguage } = useContext(LanguageContext)
  return { language, setLanguage }
}

export function useText(): Messages {
  return useContext(LanguageContext).text
}
