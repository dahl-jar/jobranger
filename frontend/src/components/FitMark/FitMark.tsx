import { useText } from '@/hooks/useLanguage'
import type { Fit } from '@/contract/payload.gen'
import { Icon, type IconName } from '@/components/Icon/Icon'
import styles from '@/components/AdMarkStyles/adMarks.module.css'

const FIT_ICONS: Record<Fit, IconName> = { strong: 'strong', partial: 'partial', on_request: 'eye' }

export function FitMark({ fit }: { fit: Fit }) {
  return (
    <span className={`${styles.fit} ${styles[fit]}`}>
      <Icon name={FIT_ICONS[fit]} />
      {useText().ui.fit[fit]}
    </span>
  )
}
