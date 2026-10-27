import { countStatuses, listedApplications } from '@/features/applications/applicationStatus'
import styles from '@/features/applications/ApplicationsSummary.module.css'
import type { Application } from '@/features/relay/contract/payload.gen'
import { formatDay, formatDayInSentence } from '@/shared/format/dates'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { SectionLabel } from '@/shared/ui/SectionLabel'

function sentSince(applications: readonly Application[], today: string, text: Messages): string {
  const first = applications.reduce((earliest, application) =>
    Date.parse(application.applied_at) < Date.parse(earliest.applied_at) ? application : earliest,
  )
  const words = text.applications.summary
  if (formatDay(first.applied_at, today, text) === text.dates.today) return words.sentToday
  return words.since(formatDayInSentence(first.applied_at, today, text))
}

interface ApplicationsSummaryProps {
  applications: readonly Application[]
  total?: number | undefined
  today: string
  pendingId?: number | undefined
}

export function ApplicationsSummary({ applications, total, today, pendingId }: ApplicationsSummaryProps) {
  const text = useText()
  const words = text.applications
  const listed = listedApplications(applications, pendingId)
  if (listed.length === 0) {
    return (
      <section className={styles.summary}>
        <SectionLabel>{words.heading}</SectionLabel>
        <p className={styles.empty}>{words.summary.empty}</p>
      </section>
    )
  }
  const counts = countStatuses(listed)
  const isTruncated = total !== undefined && total > applications.length
  const interviews = counts.interview + counts.offer + counts.accepted
  const legend = [
    { key: 'waiting', label: words.summary.waiting, count: counts.waiting, tone: styles.waiting },
    { key: 'rejected', label: words.summary.rejected, count: counts.rejected, tone: styles.rejected },
    { key: 'interviews', label: words.summary.interviews, count: interviews, tone: styles.interviews },
  ]

  return (
    <section className={styles.summary}>
      <SectionLabel>{words.heading}</SectionLabel>
      <div className={styles.total}>
        <span className={styles.count}>{isTruncated ? total : listed.length}</span>
        <span className={styles.since}>{isTruncated ? words.summary.sentInAll : sentSince(listed, today, text)}</span>
      </div>
      {isTruncated && <p className={styles.latest}>{words.summary.ofLatest(listed.length)}</p>}
      <div role="img" aria-label={words.summary.bar(counts.waiting, counts.rejected, interviews)} className={styles.bar}>
        <div className={styles.waiting} style={{ flexGrow: counts.waiting }} />
        <div className={styles.interviews} style={{ flexGrow: interviews }} />
        <div className={styles.rejected} style={{ flexGrow: counts.rejected }} />
      </div>
      <ul className={styles.legend}>
        {legend.map((item) => (
          <li key={item.key} className={styles.legendRow}>
            <span className={`${styles.swatch} ${item.tone}`} />
            <span className={styles.legendLabel}>{item.label}</span>
            <span className={styles.legendCount}>{item.count}</span>
          </li>
        ))}
      </ul>
    </section>
  )
}
