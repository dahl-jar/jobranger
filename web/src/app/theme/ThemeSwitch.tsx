import type { ThemeChoice } from '@/app/theme/theme'
import { useTheme } from '@/app/theme/themeContext'
import styles from '@/app/theme/ThemeSwitch.module.css'
import { Icon, type IconName } from '@/shared/ui/Icon'

const THEME_OPTIONS: readonly { choice: ThemeChoice; label: string; icon: IconName }[] = [
  { choice: 'light', label: 'Light', icon: 'sun' },
  { choice: 'system', label: 'System', icon: 'monitor' },
  { choice: 'dark', label: 'Dark', icon: 'moon' },
]

export function ThemeSwitch({ variant }: { variant: 'icons' | 'labeled' }) {
  const { choice, setChoice } = useTheme()
  const isLabeled = variant === 'labeled'

  return (
    <div role="group" aria-label="Theme" className={styles.group}>
      {THEME_OPTIONS.map((option) => (
        <button
          key={option.choice}
          type="button"
          aria-pressed={choice === option.choice}
          aria-label={isLabeled ? undefined : `${option.label} theme`}
          className={`${styles.option} ${isLabeled ? styles.labeled : styles.icon}`}
          onClick={() => setChoice(option.choice)}
        >
          <Icon name={option.icon} />
          {isLabeled && option.label}
        </button>
      ))}
    </div>
  )
}
