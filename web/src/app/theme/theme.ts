export type ThemeChoice = 'light' | 'system' | 'dark'
type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'jobranger.theme'
const PREFERS_LIGHT_QUERY = '(prefers-color-scheme: light)'

const DEFAULT_THEME: ThemeChoice = 'dark'
const THEME_CHOICES: readonly string[] = ['light', 'system', 'dark'] satisfies ThemeChoice[]

function isThemeChoice(value: string | null): value is ThemeChoice {
  return THEME_CHOICES.some((choice) => choice === value)
}

export function readStoredTheme(): ThemeChoice {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY)
    return isThemeChoice(stored) ? stored : DEFAULT_THEME
  } catch {
    return DEFAULT_THEME
  }
}

export function resolveTheme(choice: ThemeChoice, prefersLight: boolean): ResolvedTheme {
  if (choice !== 'system') return choice
  return prefersLight ? 'light' : 'dark'
}

export function paintTheme(choice: ThemeChoice): void {
  const prefersLight = window.matchMedia(PREFERS_LIGHT_QUERY).matches
  document.documentElement.dataset.theme = resolveTheme(choice, prefersLight)
}

export function saveTheme(choice: ThemeChoice): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, choice)
  } catch {}
}

export function watchStoredTheme(onChange: () => void): () => void {
  window.addEventListener('storage', onChange)
  return () => window.removeEventListener('storage', onChange)
}

export function watchSystemTheme(onChange: () => void): () => void {
  const media = window.matchMedia(PREFERS_LIGHT_QUERY)
  media.addEventListener('change', onChange)
  return () => media.removeEventListener('change', onChange)
}
