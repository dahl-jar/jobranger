import styles from '@/components/ProgressBar/ProgressBar.module.css'
import { PERCENT_MAX } from '@/constants/percentMax'

type ProgressTone = 'ok' | 'near'

interface ProgressBarProps {
  label: string
  percent: number
  tone?: ProgressTone
  className?: string | undefined
}

export function ProgressBar({ label, percent, tone = 'ok', className }: ProgressBarProps) {
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={PERCENT_MAX}
      aria-valuenow={percent}
      className={[styles.track, className].filter(Boolean).join(' ')}
    >
      <div className={styles[tone]} style={{ width: `${percent}%` }} />
    </div>
  )
}
