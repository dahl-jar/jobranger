import styles from '@/features/jobs/AdRow.module.css'
import { MARK_LABELS, roleLabel, SOURCE_NAMES } from '@/features/jobs/labels'
import type { ActiveAd, Mark, RoleOption } from '@/features/relay/contract/payload.gen'
import { formatDay, formatDue } from '@/shared/format/dates'
import { FitMark } from '@/shared/ui/FitMark'
import { Icon } from '@/shared/ui/Icon'

const RELATIVE_DAY = /^[A-Za-z]/

const MARK_CLASSES: Record<Mark, string | undefined> = {
  applied: styles.markApplied,
  skipped: styles.markSkipped,
  not_for_me: styles.markNotForMe,
}

function markLabel(mark: Mark, markedAt: string | null, today: string): string {
  if (mark !== 'applied' || markedAt === null) return MARK_LABELS[mark]
  const day = formatDay(markedAt, today)
  return `${MARK_LABELS.applied} ${RELATIVE_DAY.test(day) ? day.toLowerCase() : day}`
}

export function AdSide({ ad, today }: { ad: ActiveAd; today: string }) {
  const { due, note } = formatDue(ad.deadline, today)
  return (
    <div className={styles.side}>
      <div className={styles.due}>{due}</div>
      <div className={styles.dueNote}>{note}</div>
      {ad.shown_anyway && <div className={styles.dueNote}>shown by you</div>}
      {ad.is_new && <div className={styles.isNew}>New, just now</div>}
      <div className={styles.fit}>
        <FitMark fit={ad.fit} />
      </div>
      {ad.mark !== null && (
        <div className={`${styles.markLabel} ${MARK_CLASSES[ad.mark] ?? ''}`}>
          <Icon name="pencil" />
          {markLabel(ad.mark, ad.marked_at, today)}
        </div>
      )}
    </div>
  )
}

export function AdFacts({ ad, roles }: { ad: ActiveAd; roles: readonly RoleOption[] }) {
  return (
    <div className={styles.facts}>
      {ad.place !== '' && (
        <span className={styles.fact}>
          <Icon name="place" />
          {ad.place}
        </span>
      )}
      <span className={styles.fact}>
        <Icon name="area" />
        {roleLabel(ad.role_type, roles)}
      </span>
      <span className={styles.fact}>
        <Icon name="source" />
        {SOURCE_NAMES[ad.source]}
      </span>
    </div>
  )
}

export function MarkError({ error }: { error: string | undefined }) {
  if (error === undefined) return null
  return (
    <p role="alert" className={styles.error}>
      Not saved: {error}
    </p>
  )
}
