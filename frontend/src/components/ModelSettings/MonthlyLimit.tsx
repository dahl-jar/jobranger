import { SettingsFieldset } from '@/components/SettingsFieldset/SettingsFieldset'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { limitPercent } from '@/helpers/summarizeUsage'
import { formatUsd } from '@/helpers/formatUsd'
import type { Messages } from '@/i18n/en'
import { useText } from '@/hooks/useLanguage'

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
