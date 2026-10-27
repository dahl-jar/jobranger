import type { AdCallbacks } from '@/features/jobs/AdActions'
import { AdRow } from '@/features/jobs/AdRow'
import styles from '@/features/jobs/JobsView.module.css'
import type { JobList } from '@/features/jobs/selectors'
import type { ActiveAd, RoleOption } from '@/features/relay/contract/payload.gen'

interface AdListProps extends AdCallbacks {
  ads: readonly ActiveAd[]
  today: string
  roles: readonly RoleOption[]
  from: JobList
  disabled: boolean
  errors: Readonly<Record<string, string>>
}

export function AdList({ ads, today, roles, from, disabled, errors, onMark, onUnmark }: AdListProps) {
  if (ads.length === 0) {
    return <p className={styles.empty}>No matches here for the roles you picked.</p>
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
