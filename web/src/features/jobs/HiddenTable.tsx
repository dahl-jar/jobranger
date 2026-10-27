import { useId } from 'react'
import styles from '@/features/jobs/tables.module.css'
import { capitalize, SOURCE_NAMES } from '@/features/jobs/labels'
import { hiddenBreakdown, selectHidden } from '@/features/jobs/selectors'
import type { AdsSnapshot, HiddenAd, HiddenKind, YearBand } from '@/features/relay/contract/payload.gen'
import { Icon } from '@/shared/ui/Icon'

const LOOSENING_KINDS: ReadonlySet<HiddenKind> = new Set(['years', 'senior', 'place'])
const LOOSENS_NOTE = 'Show anyway also loosens this limit one step from the next run.'
const ONLY_THIS_NOTE = 'Show anyway shows only this ad.'

interface HiddenTableProps {
  snapshot: AdsSnapshot
  reason: HiddenKind | undefined
  bands: readonly YearBand[] | undefined
  movedIds: ReadonlySet<string>
  disabled: boolean
  onReasonChange: (reason: HiddenKind | undefined) => void
  onShowAnyway: (adId: string, on: boolean) => void
}

function ReasonPill({ label, count, isPicked, onPick }: { label: string; count: number; isPicked: boolean; onPick: () => void }) {
  return (
    <button type="button" aria-pressed={isPicked} className={styles.pill} onClick={onPick}>
      {label} {count}
    </button>
  )
}

interface HiddenRowProps {
  ad: HiddenAd
  isMoved: boolean
  disabled: boolean
  onShowAnyway: HiddenTableProps['onShowAnyway']
}

function HiddenRow({ ad, isMoved, disabled, onShowAnyway }: HiddenRowProps) {
  const titleId = useId()
  return (
    <div role="row" className={`${styles.hiddenRow} ${isMoved ? styles.shown : ''}`}>
      <span role="cell" className={styles.reason}>
        <span className={styles.reasonText}>
          <Icon name="notForMe" />
          {ad.reason}
        </span>
        <span className={styles.note}>{LOOSENING_KINDS.has(ad.kind) ? LOOSENS_NOTE : ONLY_THIS_NOTE}</span>
      </span>
      <span role="cell" className={styles.company}>
        {ad.company}
      </span>
      <span role="cell" id={titleId} className={styles.role}>
        {ad.title}
      </span>
      <span role="cell" className={styles.source}>
        {SOURCE_NAMES[ad.source]}
      </span>
      <span role="cell">
        <button
          type="button"
          aria-pressed={isMoved}
          aria-describedby={titleId}
          disabled={disabled}
          className={`${styles.showAnyway} ${isMoved ? styles.moved : ''}`}
          onClick={() => onShowAnyway(ad.id, !isMoved)}
        >
          <Icon name={isMoved ? 'check' : 'eye'} />
          {isMoved ? 'Moved to Active' : 'Show anyway'}
        </button>
      </span>
    </div>
  )
}

export function HiddenTable({ snapshot, reason, bands, movedIds, disabled, onReasonChange, onShowAnyway }: HiddenTableProps) {
  const breakdown = hiddenBreakdown(snapshot, bands)
  const rows = selectHidden(snapshot, reason)
  const total = reason === undefined ? breakdown.total : snapshot.hidden_counts[reason]
  return (
    <>
      <div className={styles.pills}>
        <ReasonPill label="All" count={breakdown.total} isPicked={reason === undefined} onPick={() => onReasonChange(undefined)} />
        {breakdown.parts.map((part) => (
          <ReasonPill
            key={part.kind}
            label={capitalize(part.label)}
            count={part.count}
            isPicked={reason === part.kind}
            onPick={() => onReasonChange(part.kind)}
          />
        ))}
      </div>
      <div className={styles.scroll}>
        <div role="table" aria-label="Hidden ads" className={styles.table}>
          <div role="row" className={`${styles.hiddenRow} ${styles.head}`}>
            <span role="columnheader">Hidden because</span>
            <span role="columnheader">Company</span>
            <span role="columnheader">Role</span>
            <span role="columnheader">Source</span>
            <span role="columnheader">
              <span className="visually-hidden">Action</span>
            </span>
          </div>
          {rows.map((ad) => (
            <HiddenRow key={ad.id} ad={ad} isMoved={movedIds.has(ad.id)} disabled={disabled} onShowAnyway={onShowAnyway} />
          ))}
        </div>
      </div>
      <p className={styles.footer}>
        Showing {rows.length} of {total}. "Show anyway" moves the ad to Active.
      </p>
    </>
  )
}
