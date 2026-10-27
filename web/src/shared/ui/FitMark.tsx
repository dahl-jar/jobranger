import { FIT_LABELS, type Fit } from '@/shared/ui/fit'
import { Icon, type IconName } from '@/shared/ui/Icon'
import styles from '@/shared/ui/marks.module.css'

const FIT_ICONS: Record<Fit, IconName> = { strong: 'strong', partial: 'partial', on_request: 'eye' }

export function FitMark({ fit }: { fit: Fit }) {
  return (
    <span className={`${styles.fit} ${styles[fit]}`}>
      <Icon name={FIT_ICONS[fit]} />
      {FIT_LABELS[fit]}
    </span>
  )
}
