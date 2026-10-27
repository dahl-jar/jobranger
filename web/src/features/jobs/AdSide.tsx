import styles from '@/features/jobs/AdRow.module.css'
import { roleLabel } from '@/features/jobs/labels'
import type { ActiveAd, Mark, RoleOption } from '@/features/relay/contract/payload.gen'
import { formatDayInSentence, formatDue } from '@/shared/format/dates'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { FitMark } from '@/shared/ui/FitMark'
import { Icon } from '@/shared/ui/Icon'
import { SOURCE_NAMES } from '@/shared/sources'

const MARK_CLASSES: Record<Mark, string | undefined> = {
  applied: styles.markApplied,
  skipped: styles.markSkipped,
  not_for_me: styles.markNotForMe,
}

function markLabel(mark: Mark, markedAt: string | null, today: string, text: Messages): string {
  if (mark !== 'applied' || markedAt === null) return text.jobs.marks[mark]
  return text.jobs.appliedOn(formatDayInSentence(markedAt, today, text))
}

export function AdSide({ ad, today }: { ad: ActiveAd; today: string }) {
  const text = useText()
  const { due, note } = formatDue(ad.deadline, today, text)
  return (
    <div className={styles.side}>
      <div className={styles.due}>{due}</div>
      <div className={styles.dueNote}>{note}</div>
      {ad.shown_anyway && <div className={styles.dueNote}>{text.jobs.shownByYou}</div>}
      {ad.is_new && <div className={styles.isNew}>{text.jobs.isNew}</div>}
      <div className={styles.fit}>
        <FitMark fit={ad.fit} />
      </div>
      {ad.mark !== null && (
        <div className={`${styles.markLabel} ${MARK_CLASSES[ad.mark] ?? ''}`}>
          <Icon name="pencil" />
          {markLabel(ad.mark, ad.marked_at, today, text)}
        </div>
      )}
    </div>
  )
}

export function AdFacts({ ad, roles }: { ad: ActiveAd; roles: readonly RoleOption[] }) {
  const text = useText()

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
        {roleLabel(ad.role_type, roles, text)}
      </span>
      <span className={styles.fact}>
        <Icon name="source" />
        {SOURCE_NAMES[ad.source]}
      </span>
    </div>
  )
}

export function MarkError({ error }: { error: string | undefined }) {
  const notSaved = useText().common.notSaved
  if (error === undefined) return null
  return (
    <p role="alert" className={styles.error}>
      {notSaved(error)}
    </p>
  )
}
