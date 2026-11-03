import type { ReactNode } from 'react'
import { Link } from 'react-router'
import styles from '@/components/JobsView/adTables.module.css'
import { closedWhy } from '@/helpers/adLabels'
import { useAppBase } from '@/hooks/useAppBase'
import { detailPath } from '@/helpers/adPaths'
import { selectActive, selectClosed } from '@/helpers/adSelectors'
import type { AdsSnapshot, SourceId } from '@/contract/payload.gen'
import { formatDay, formatDue } from '@/helpers/formatDate'
import type { Messages } from '@/i18n/en'
import { useText } from '@/hooks/useLanguage'
import { SOURCE_NAMES } from '@/constants/sourceNames'

interface AllRow {
  key: string
  due: string
  company: string
  role: ReactNode
  state: string
  source: SourceId
  isActive: boolean
}

function allRows(snapshot: AdsSnapshot, text: Messages, base: string): AllRow[] {
  const today = snapshot.as_of
  return [
    ...selectActive(snapshot).map((ad) => ({
      key: `active-${ad.id}`,
      due: formatDue(ad.deadline, today, text).due,
      company: ad.company,
      role: (
        <Link to={detailPath(ad.id, base)} state={{ from: 'all' }} className={styles.roleLink}>
          {ad.title}
        </Link>
      ),
      state: text.ui.fit[ad.fit],
      source: ad.source,
      isActive: true,
    })),
    ...snapshot.hidden.map((ad) => ({
      key: `hidden-${ad.id}`,
      due: '',
      company: ad.company,
      role: ad.title,
      state: text.jobs.hiddenState(ad.reason),
      source: ad.source,
      isActive: false,
    })),
    ...selectClosed(snapshot).map((ad) => ({
      key: `closed-${ad.id}`,
      due: formatDay(ad.closed_on, today, text),
      company: ad.company,
      role: ad.title,
      state: text.jobs.closedState(closedWhy(ad, text)),
      source: ad.source,
      isActive: false,
    })),
  ]
}

export function AllAdsTable({ snapshot }: { snapshot: AdsSnapshot }) {
  const text = useText()
  const words = text.jobs
  const rows = allRows(snapshot, text, useAppBase())
  return (
    <>
      <div className={styles.scroll}>
        <div role="table" aria-label={words.allLabel} className={styles.table}>
          <div role="row" className={`${styles.allRow} ${styles.head}`}>
            <span role="columnheader">{words.columns.deadline}</span>
            <span role="columnheader">{words.columns.company}</span>
            <span role="columnheader">{words.columns.role}</span>
            <span role="columnheader">{words.columns.state}</span>
            <span role="columnheader">{words.columns.source}</span>
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
      <p className={styles.footer}>{words.allFooter(rows.length)}</p>
    </>
  )
}
