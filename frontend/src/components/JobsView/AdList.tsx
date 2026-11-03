import type { AdCallbacks } from '@/components/AdRow/AdActions'
import { AdRow } from '@/components/AdRow/AdRow'
import styles from '@/components/JobsView/JobsView.module.css'
import type { JobList } from '@/helpers/adSelectors'
import type { ActiveAd, RoleOption } from '@/contract/payload.gen'
import { useText } from '@/hooks/useLanguage'

interface AdListProps extends AdCallbacks {
  ads: readonly ActiveAd[]
  today: string
  roles: readonly RoleOption[]
  from: JobList
  disabled: boolean
  errors: Readonly<Record<string, string>>
}

export function AdList({ ads, today, roles, from, disabled, errors, onMark, onUnmark }: AdListProps) {
  const empty = useText().jobs.empty
  if (ads.length === 0) {
    return <p className={styles.empty}>{empty}</p>
  }
  return ads.map((ad) => (
    <AdRow
      key={ad.id}
      ad={ad}
      today={today}
      roles={roles}
      from={from}
      disabled={disabled}
      error={errors[ad.id]}
      onMark={onMark}
      onUnmark={onUnmark}
    />
  ))
}
