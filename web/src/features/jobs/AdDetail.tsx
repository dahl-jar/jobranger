import { type ReactNode, useId } from 'react'
import { Link } from 'react-router'
import styles from '@/features/jobs/AdRow.module.css'
import { AdActions, type AdCallbacks } from '@/features/jobs/AdActions'
import { AdFacts, AdSide, MarkError } from '@/features/jobs/AdSide'
import { LIST_LABELS, SOURCE_NAMES } from '@/features/jobs/labels'
import { listPath } from '@/features/jobs/paths'
import type { ActiveAd, RoleOption } from '@/features/relay/contract/payload.gen'
import { CheckList } from '@/shared/ui/CheckList'
import { Icon } from '@/shared/ui/Icon'
import { SectionLabel } from '@/shared/ui/SectionLabel'

type BackList = 'today' | 'active' | 'all'

interface AdDetailProps extends AdCallbacks {
  ad: ActiveAd | undefined
  today: string
  roles: readonly RoleOption[]
  from: unknown
  disabled: boolean
  error: string | undefined
  notice?: ReactNode
}

function backList(from: unknown): BackList {
  return from === 'today' || from === 'all' ? from : 'active'
}

function DetailSection({ label, children }: { label: string; children: ReactNode }) {
  return (
    <section className={styles.section}>
      <SectionLabel>{label}</SectionLabel>
      {children}
    </section>
  )
}

export function AdDetail({ ad, today, roles, from, disabled, error, notice, onMark, onUnmark }: AdDetailProps) {
  const back = backList(from)
  const titleId = useId()
  return (
    <>
      <Link to={listPath(back)} className={styles.back}>
        <Icon name="back" />
        Back to {LIST_LABELS[back]}
      </Link>
      {notice}
      {ad === undefined ? (
        <div className={styles.gone}>
          <p>This ad is no longer in your list.</p>
          <Link to={listPath('active')}>See Active</Link>
        </div>
      ) : (
        <article className={`${styles.row} ${styles.detail}`}>
          <AdSide ad={ad} today={today} />
          <div className={styles.body}>
            <div className={styles.company}>{ad.company}</div>
            <h1 id={titleId} className={styles.detailTitle}>{ad.title}</h1>
            <AdFacts ad={ad} roles={roles} />
            {ad.summary !== '' && (
              <DetailSection label="What the role is about">
                <p className={styles.summary}>{ad.summary}</p>
              </DetailSection>
            )}
            {ad.checks.length > 0 && (
              <DetailSection label="What the ad asks for">
                <CheckList checks={ad.checks} showWord />
              </DetailSection>
            )}
            <DetailSection label="Why it fits you">
              <p className={styles.reason}>{ad.reason}</p>
            </DetailSection>
            {ad.gaps.length > 0 && (
              <DetailSection label="Gaps">
                <ul className={styles.gaps}>
                  {ad.gaps.map((gap) => (
                    <li key={gap}>{gap}</li>
                  ))}
                </ul>
              </DetailSection>
            )}
            <div className={styles.actions}>
              <a href={ad.url} target="_blank" rel="noopener noreferrer" className={styles.details}>
                Open on {SOURCE_NAMES[ad.source]}
                <Icon name="external" />
              </a>
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
      )}
    </>
  )
}
