import { countStatuses, listedApplications } from '@/features/applications/applicationStatus'
import styles from '@/features/applications/ApplicationsSummary.module.css'
import type { Application } from '@/features/relay/contract/payload.gen'
import { formatDay, TODAY_LABEL, YESTERDAY_LABEL } from '@/shared/format/dates'
import { SectionLabel } from '@/shared/ui/SectionLabel'

const RELATIVE_DAYS = new Set([TODAY_LABEL, YESTERDAY_LABEL])

function sinceDay(applications: readonly Application[], today: string): string {
  const first = applications.reduce((earliest, application) =>
    Date.parse(application.applied_at) < Date.parse(earliest.applied_at) ? application : earliest,
  )
  const day = formatDay(first.applied_at, today)
  return RELATIVE_DAYS.has(day) ? day.toLowerCase() : day
}

interface ApplicationsSummaryProps {
  applications: readonly Application[]
  today: string
  pendingId?: number | undefined
}

export function ApplicationsSummary({ applications, today, pendingId }: ApplicationsSummaryProps) {
  const listed = listedApplications(applications, pendingId)
  if (listed.length === 0) {
    return (
      <section className={styles.summary}>
        <SectionLabel>Applications</SectionLabel>
        <p className={styles.empty}>No applications yet.</p>
      </section>
    )
  }
  const counts = countStatuses(listed)
  const interviews = counts.interview + counts.offer + counts.accepted
  const legend = [
    { label: 'Waiting', count: counts.waiting, tone: styles.waiting },
    { label: 'Rejected', count: counts.rejected, tone: styles.rejected },
    { label: 'Interviews', count: interviews, tone: styles.interviews },
  ]

  return (
    <section className={styles.summary}>
      <SectionLabel>Applications</SectionLabel>
      <div className={styles.total}>
        <span className={styles.count}>{listed.length}</span>
        <span className={styles.since}>sent since {sinceDay(listed, today)}</span>
      </div>
      <div role="img" aria-label={`${counts.waiting} waiting, ${counts.rejected} rejected, ${interviews} interviews`} className={styles.bar}>
        <div className={styles.waiting} style={{ flexGrow: counts.waiting }} />
        <div className={styles.interviews} style={{ flexGrow: interviews }} />
        <div className={styles.rejected} style={{ flexGrow: counts.rejected }} />
      </div>
      <ul className={styles.legend}>
        {legend.map((item) => (
          <li key={item.label} className={styles.legendRow}>
            <span className={`${styles.swatch} ${item.tone}`} />
            <span className={styles.legendLabel}>{item.label}</span>
            <span className={styles.legendCount}>{item.count}</span>
          </li>
        ))}
      </ul>
    </section>
  )
}
