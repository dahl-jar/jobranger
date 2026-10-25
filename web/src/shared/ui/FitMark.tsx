import { FIT_LABELS, type Fit } from '@/shared/ui/fit'
import { Icon } from '@/shared/ui/Icon'
import styles from '@/shared/ui/marks.module.css'

export function FitMark({ fit }: { fit: Fit }) {
  return (
    <span className={`${styles.fit} ${styles[fit]}`}>
      <Icon name={fit} />
      {FIT_LABELS[fit]}
    </span>
  )
}
