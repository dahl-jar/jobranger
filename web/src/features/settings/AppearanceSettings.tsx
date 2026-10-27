import type { ThemeChoice } from '@/app/theme/theme'
import { useTheme } from '@/app/theme/themeContext'
import { CheckRow } from '@/features/settings/CheckRow'
import { SettingsFieldset } from '@/features/settings/SettingsFieldset'
import styles from '@/features/settings/Settings.module.css'

const THEME_OPTIONS: readonly { choice: ThemeChoice; label: string; hint: string }[] = [
  { choice: 'light', label: 'Light', hint: 'Warm paper' },
  { choice: 'system', label: 'System', hint: 'Follows your computer' },
  { choice: 'dark', label: 'Dark', hint: 'Default' },
]

export function AppearanceSettings() {
  const { choice, setChoice } = useTheme()

  return (
    <>
      <SettingsFieldset legend="Theme">
        <div className={styles.stack}>
          {THEME_OPTIONS.map((option) => (
            <CheckRow
              key={option.choice}
              type="radio"
              name="theme"
              label={option.label}
              hint={option.hint}
              checked={choice === option.choice}
              onChange={() => setChoice(option.choice)}
            />
          ))}
        </div>
      </SettingsFieldset>
      <p className={styles.note}>The email is always light.</p>
    </>
  )
}
