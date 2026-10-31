import styles from '@/features/jobs/tables.module.css'
import { closedWhy } from '@/features/jobs/labels'
import { selectClosed } from '@/features/jobs/selectors'
import type { AdsSnapshot, ClosedMark } from '@/features/relay/contract/payload.gen'
import { formatDay } from '@/shared/format/dates'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

function YouCell({ mark }: { mark: ClosedMark }) {
  const words = useText().jobs
  const isApplied = mark === 'applied'
  return (
    <span role="cell" className={`${styles.you} ${isApplied ? styles.youApplied : ''}`}>
      <Icon name={isApplied ? 'check' : 'dash'} />
      {mark === 'none' ? words.noAction : words.marks[mark]}
    </span>
  )
}

export function ClosedTable({ snapshot }: { snapshot: AdsSnapshot }) {
  const text = useText()
  const words = text.jobs

  return (
    <>
      <div className={styles.scroll}>
        <div role="table" aria-label={words.closedLabel} className={styles.table}>
          <div role="row" className={`${styles.closedRow} ${styles.head}`}>
            <span role="columnheader">{words.columns.closed}</span>
            <span role="columnheader">{words.columns.company}</span>
            <span role="columnheader">{words.columns.role}</span>
            <span role="columnheader">{words.columns.why}</span>
            <span role="columnheader">{words.columns.you}</span>
          </div>
          {selectClosed(snapshot).map((ad) => (
            <div key={ad.id} role="row" className={styles.closedRow}>
              <span role="cell" className={styles.date}>
                {formatDay(ad.closed_on, snapshot.as_of, text)}
              </span>
              <span role="cell" className={styles.company}>
                {ad.company}
              </span>
              <span role="cell" className={styles.role}>
                {ad.title}
              </span>
              <span role="cell" className={styles.why}>
                {closedWhy(ad, text)}
              </span>
              <YouCell mark={ad.mark} />
            </div>
          ))}
        </div>
      </div>
      <p className={styles.footer}>{words.closedFooter}</p>
    </>
  )
}
