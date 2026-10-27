import type { ThemeChoice } from '@/app/theme/theme'
import { useTheme } from '@/app/theme/themeContext'
import styles from '@/app/theme/ThemeSwitch.module.css'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Icon, type IconName } from '@/shared/ui/Icon'

type ThemeWords = Messages['app']['theme']

const THEME_OPTIONS: readonly { choice: ThemeChoice; label: keyof ThemeWords; iconLabel: keyof ThemeWords; icon: IconName }[] = [
  { choice: 'light', label: 'light', iconLabel: 'lightTheme', icon: 'sun' },
  { choice: 'system', label: 'system', iconLabel: 'systemTheme', icon: 'monitor' },
  { choice: 'dark', label: 'dark', iconLabel: 'darkTheme', icon: 'moon' },
]

export function ThemeSwitch({ variant }: { variant: 'icons' | 'labeled' }) {
  const { choice, setChoice } = useTheme()
  const words = useText().app.theme
  const isLabeled = variant === 'labeled'

  return (
    <div role="group" aria-label={words.group} className={styles.group}>
      {THEME_OPTIONS.map((option) => (
        <button
          key={option.choice}
          type="button"
          aria-pressed={choice === option.choice}
          aria-label={isLabeled ? undefined : words[option.iconLabel]}
          className={`${styles.option} ${isLabeled ? styles.labeled : styles.icon}`}
          onClick={() => setChoice(option.choice)}
        >
          <Icon name={option.icon} />
          {isLabeled && words[option.label]}
        </button>
      ))}
    </div>
  )
}
