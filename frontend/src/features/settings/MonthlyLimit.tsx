import { SettingsFieldset } from '@/features/settings/SettingsFieldset'
import styles from '@/features/settings/Settings.module.css'
import { limitPercent } from '@/features/usage/usageSummary'
import { formatUsd } from '@/shared/format/money'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'

const MONTHLY_LIMIT_OPTIONS_USD: readonly (number | null)[] = [null, 1, 3, 5, 10]

function limitOptions(limitUsd: number | null): readonly (number | null)[] {
  if (MONTHLY_LIMIT_OPTIONS_USD.includes(limitUsd)) return MONTHLY_LIMIT_OPTIONS_USD
  return [...MONTHLY_LIMIT_OPTIONS_USD, limitUsd].sort((first, second) => (first ?? 0) - (second ?? 0))
}

function limitNote(limitUsd: number | null, monthUsd: number, text: Messages): string {
  if (limitUsd === null) return text.settings.limit.noneSet
  return text.settings.limit.used(text.numbers.percent(limitPercent(monthUsd, limitUsd)))
}

interface MonthlyLimitProps {
  limitUsd: number | null
  monthUsd: number
  onChange: (limitUsd: number | null) => void
}

export function MonthlyLimit({ limitUsd, monthUsd, onChange }: MonthlyLimitProps) {
  const text = useText()

  return (
    <SettingsFieldset legend={text.settings.limit.legend}>
      <div className={styles.pills}>
        {limitOptions(limitUsd).map((option) => (
          <label key={String(option)} className={styles.pill}>
            <input type="radio" name="monthly-limit" checked={option === limitUsd} onChange={() => onChange(option)} />
            {option === null ? text.settings.limit.none : formatUsd(option, text)}
          </label>
        ))}
      </div>
      <p className={styles.note}>{limitNote(limitUsd, monthUsd, text)}</p>
    </SettingsFieldset>
  )
}
