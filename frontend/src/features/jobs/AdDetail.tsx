import { type ReactNode, useId } from 'react'
import { Link } from 'react-router'
import styles from '@/features/jobs/AdRow.module.css'
import { AdActions, type AdCallbacks } from '@/features/jobs/AdActions'
import { AdFacts, AdSide, MarkError } from '@/features/jobs/AdSide'
import { useAppBase } from '@/app/paths'
import { listPath } from '@/features/jobs/paths'
import type { ActiveAd, RoleOption } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { CheckList } from '@/shared/ui/CheckList'
import { Icon } from '@/shared/ui/Icon'
import { SectionLabel } from '@/shared/ui/SectionLabel'
import { SOURCE_NAMES } from '@/shared/labels/sources'

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
  const words = useText().jobs
  const base = useAppBase()
  return (
    <>
      <Link to={listPath(back, base)} className={styles.back}>
        <Icon name="back" />
        {words.backTo(words.lists[back])}
      </Link>
      {notice}
      {ad === undefined ? (
        <div className={styles.gone}>
          <h1 className={styles.goneTitle}>{words.goneTitle}</h1>
          <p>{words.gone}</p>
          <Link to={listPath('active', base)}>{words.seeActive}</Link>
        </div>
      ) : (
        <article className={`${styles.row} ${styles.detail}`}>
          <AdSide ad={ad} today={today} />
          <div className={styles.body}>
            <div className={styles.company}>{ad.company}</div>
            <h1 id={titleId} className={styles.detailTitle}>{ad.title}</h1>
            <AdFacts ad={ad} roles={roles} />
            {ad.summary !== '' && (
              <DetailSection label={words.about}>
                <p className={styles.summary}>{ad.summary}</p>
              </DetailSection>
            )}
            {ad.checks.length > 0 && (
              <DetailSection label={words.asks}>
                <CheckList checks={ad.checks} showWord />
              </DetailSection>
            )}
            <DetailSection label={words.why}>
              <p className={styles.reason}>{ad.reason}</p>
            </DetailSection>
            {ad.gaps.length > 0 && (
              <DetailSection label={words.gaps}>
                <ul className={styles.gaps}>
                  {ad.gaps.map((gap) => (
                    <li key={gap}>{gap}</li>
                  ))}
                </ul>
              </DetailSection>
            )}
            <div className={styles.actions}>
              <a href={ad.url} target="_blank" rel="noopener noreferrer" className={styles.details}>
                {words.openOn(SOURCE_NAMES[ad.source])}
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
