export type Language = 'en' | 'nb'

export const LANGUAGES: readonly Language[] = ['en', 'nb']
export const LANGUAGE_STORAGE_KEY = 'jobranger.language'

export const LANGUAGE_NAMES: Record<Language, string> = {
  en: 'English',
  nb: 'Norsk bokmål',
}

const DEFAULT_LANGUAGE: Language = 'en'
const NORWEGIAN_CODES: ReadonlySet<string> = new Set(['nb', 'no', 'nn'])
const SUBTAG_SEPARATOR = '-'

function isLanguage(value: string | null): value is Language {
  return LANGUAGES.some((language) => language === value)
}

function languageFromLocale(locale: string): Language {
  const [primary = ''] = locale.toLowerCase().split(SUBTAG_SEPARATOR)
  return NORWEGIAN_CODES.has(primary) ? 'nb' : DEFAULT_LANGUAGE
}

function readStoredLanguage(): Language | undefined {
  try {
    const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY)
    return isLanguage(stored) ? stored : undefined
  } catch {
    return undefined
  }
}

export function initialLanguage(): Language {
  return readStoredLanguage() ?? languageFromLocale(navigator.language)
}

export function saveLanguage(language: Language): void {
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, language)
  } catch {}
}
