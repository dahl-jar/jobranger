import { useId } from 'react'
import { Link } from 'react-router'
import { AdActions, type AdCallbacks } from '@/features/jobs/AdActions'
import styles from '@/features/jobs/AdRow.module.css'
import { AdFacts, AdSide, MarkError } from '@/features/jobs/AdSide'
import { useAppBase } from '@/app/paths'
import { detailPath } from '@/features/jobs/paths'
import type { JobList } from '@/features/jobs/selectors'
import type { ActiveAd, RoleOption } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { CheckList } from '@/shared/ui/CheckList'
import { Icon } from '@/shared/ui/Icon'

interface AdRowProps extends AdCallbacks {
  ad: ActiveAd
  today: string
  roles: readonly RoleOption[]
  from: JobList
  disabled: boolean
  error: string | undefined
}

export function AdRow({ ad, today, roles, from, disabled, error, onMark, onUnmark }: AdRowProps) {
  const titleId = useId()
  const details = useText().jobs.details
  const base = useAppBase()
  const isDimmed = ad.mark !== null && ad.mark !== 'applied'
  return (
    <article aria-labelledby={titleId} className={`${styles.row} ${isDimmed ? styles.dimmed : ''}`}>
      <AdSide ad={ad} today={today} />
      <div className={styles.body}>
        <div className={styles.company}>{ad.company}</div>
        <h2 id={titleId} className={styles.title}>
          <Link to={detailPath(ad.id, base)} state={{ from }} className={styles.titleLink}>
            {ad.title}
          </Link>
        </h2>
        <AdFacts ad={ad} roles={roles} />
        <div className={styles.about}>
          <p className={styles.why}>{ad.summary === '' ? ad.reason : ad.summary}</p>
          {ad.checks.length > 0 && (
            <div className={styles.checks}>
              <CheckList checks={ad.checks} showWord />
            </div>
          )}
        </div>
        <div className={styles.actions}>
          <Link to={detailPath(ad.id, base)} state={{ from }} className={styles.details}>
            {details}
            <Icon name="forward" />
          </Link>
          <AdActions
            adId={ad.id}
            mark={ad.mark}
            disabled={disabled}
            describedBy={titleId}
            onMark={onMark}
            onUnmark={onUnmark}
          />
        </div>
        <MarkError error={error} />
      </div>
    </article>
  )
}
