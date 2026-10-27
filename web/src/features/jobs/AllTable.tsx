import type { ReactNode } from 'react'
import { Link } from 'react-router'
import styles from '@/features/jobs/tables.module.css'
import { closedWhy, SOURCE_NAMES } from '@/features/jobs/labels'
import { detailPath } from '@/features/jobs/paths'
import { selectActive, selectClosed } from '@/features/jobs/selectors'
import type { AdsSnapshot, SourceId } from '@/features/relay/contract/payload.gen'
import { formatDay, formatDue } from '@/shared/format/dates'
import { FIT_LABELS } from '@/shared/ui/fit'

interface AllRow {
  key: string
  due: string
  company: string
  role: ReactNode
  state: string
  source: SourceId
  isActive: boolean
}

function allRows(snapshot: AdsSnapshot): AllRow[] {
  const today = snapshot.as_of
  return [
    ...selectActive(snapshot).map((ad) => ({
      key: `active-${ad.id}`,
      due: formatDue(ad.deadline, today).due,
      company: ad.company,
      role: (
        <Link to={detailPath(ad.id)} state={{ from: 'all' }} className={styles.roleLink}>
          {ad.title}
        </Link>
      ),
      state: FIT_LABELS[ad.fit],
      source: ad.source,
      isActive: true,
    })),
    ...snapshot.hidden.map((ad) => ({
      key: `hidden-${ad.id}`,
      due: '',
      company: ad.company,
      role: ad.title,
      state: `Hidden: ${ad.reason}`,
      source: ad.source,
      isActive: false,
    })),
    ...selectClosed(snapshot).map((ad) => ({
      key: `closed-${ad.id}`,
      due: formatDay(ad.closed_on, today),
      company: ad.company,
      role: ad.title,
      state: `Closed: ${closedWhy(ad)}`,
      source: ad.source,
      isActive: false,
    })),
  ]
}

export function AllTable({ snapshot }: { snapshot: AdsSnapshot }) {
  const rows = allRows(snapshot)
  return (
    <>
      <div className={styles.scroll}>
        <div role="table" aria-label="All ads" className={styles.table}>
          <div role="row" className={`${styles.allRow} ${styles.head}`}>
            <span role="columnheader">Deadline</span>
            <span role="columnheader">Company</span>
            <span role="columnheader">Role</span>
            <span role="columnheader">State</span>
            <span role="columnheader">Source</span>
          </div>
          {rows.map((row) => (
            <div key={row.key} role="row" className={styles.allRow}>
              <span role="cell" className={styles.date}>
                {row.due}
              </span>
              <span role="cell" className={styles.company}>
                {row.company}
              </span>
              <span role="cell" className={styles.role}>
                {row.role}
              </span>
              <span role="cell" className={row.isActive ? styles.state : styles.muted}>
                {row.state}
              </span>
              <span role="cell" className={styles.source}>
                {SOURCE_NAMES[row.source]}
              </span>
            </div>
          ))}
        </div>
      </div>
      <p className={styles.footer}>Showing {rows.length}. Click a match to open it.</p>
    </>
  )
}
