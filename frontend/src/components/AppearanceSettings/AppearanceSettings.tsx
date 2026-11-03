import type { ThemeChoice } from '@/helpers/applyTheme'
import { useTheme } from '@/hooks/useTheme'
import type { ConfigPatch } from '@/contract/payload.gen'
import { CheckRow } from '@/components/CheckRow/CheckRow'
import { SettingsFieldset } from '@/components/SettingsFieldset/SettingsFieldset'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { LANGUAGE_NAMES, LANGUAGES, type Language } from '@/helpers/languageChoice'
import { useLanguage, useText } from '@/hooks/useLanguage'

const THEME_CHOICES: readonly ThemeChoice[] = ['light', 'system', 'dark']

export function AppearanceSettings({ onPatch }: { onPatch?: (patch: ConfigPatch) => void }) {
  const { choice, setChoice } = useTheme()
  const { language, setLanguage } = useLanguage()
  const text = useText()
  const words = text.settings.appearance

  function pickLanguage(next: Language) {
    setLanguage(next)
    onPatch?.({ language: next })
  }

  return (
    <>
      <SettingsFieldset legend={words.theme}>
        <div className={styles.stack}>
          {THEME_CHOICES.map((option) => (
            <CheckRow
              key={option}
              type="radio"
              name="theme"
              label={text.app.theme[option]}
              hint={words.hints[option]}
              checked={choice === option}
              onChange={() => setChoice(option)}
            />
          ))}
        </div>
      </SettingsFieldset>
      <p className={styles.note}>{words.emailLight}</p>
      <SettingsFieldset legend={words.language}>
        <div className={styles.stack}>
          {LANGUAGES.map((option) => (
            <CheckRow
              key={option}
              type="radio"
              name="language"
              label={LANGUAGE_NAMES[option]}
              lang={option}
              checked={language === option}
              onChange={() => pickLanguage(option)}
            />
          ))}
        </div>
      </SettingsFieldset>
      <p className={styles.note}>{words.languageNote}</p>
    </>
  )
}
