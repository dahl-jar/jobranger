import styles from '@/features/jobs/tables.module.css'
import { CLOSED_MARK_LABELS, closedWhy } from '@/features/jobs/labels'
import { selectClosed } from '@/features/jobs/selectors'
import type { AdsSnapshot, ClosedMark } from '@/features/relay/contract/payload.gen'
import { formatDay } from '@/shared/format/dates'
import { Icon } from '@/shared/ui/Icon'

function YouCell({ mark }: { mark: ClosedMark }) {
  const isApplied = mark === 'applied'
  return (
    <span role="cell" className={`${styles.you} ${isApplied ? styles.youApplied : ''}`}>
      <Icon name={isApplied ? 'check' : 'dash'} />
      {CLOSED_MARK_LABELS[mark]}
    </span>
  )
}

export function ClosedTable({ snapshot }: { snapshot: AdsSnapshot }) {
  return (
    <>
      <div className={styles.scroll}>
        <div role="table" aria-label="Closed ads" className={styles.table}>
          <div role="row" className={`${styles.closedRow} ${styles.head}`}>
            <span role="columnheader">Closed</span>
            <span role="columnheader">Company</span>
            <span role="columnheader">Role</span>
            <span role="columnheader">Why</span>
            <span role="columnheader">You</span>
          </div>
          {selectClosed(snapshot).map((ad) => (
            <div key={ad.id} role="row" className={styles.closedRow}>
              <span role="cell" className={styles.date}>
                {formatDay(ad.closed_on, snapshot.as_of)}
              </span>
              <span role="cell" className={styles.company}>
                {ad.company}
              </span>
              <span role="cell" className={styles.role}>
                {ad.title}
              </span>
              <span role="cell" className={styles.why}>
                {closedWhy(ad)}
              </span>
              <YouCell mark={ad.mark} />
            </div>
          ))}
        </div>
      </div>
      <p className={styles.footer}>Kept for 30 days.</p>
    </>
  )
}
