import { Link } from 'react-router'
import { JOBS_PATH } from '@/app/paths'
import styles from '@/features/run/run.module.css'
import { SOURCE_LABELS, type EndedRun } from '@/features/run/runView'
import type { RunFinished } from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'
import { formatUsd } from '@/shared/format/money'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'

const OUTCOME_ICON_SIZE = 16

function countOf(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1 ? singular : plural}`
}

function DismissButton({ onDismiss }: { onDismiss: () => void }) {
  return (
    <Button variant="link" className={styles.dismiss} aria-label="Dismiss" onClick={onDismiss}>
      <Icon name="close" />
    </Button>
  )
}

function FinishedRun({ result, onDismiss }: { result: RunFinished; onDismiss: () => void }) {
  const counts = [
    countOf(result.new_ads, 'new ad', 'new ads'),
    countOf(result.matches, 'match', 'matches'),
    `${result.hidden} hidden`,
    formatUsd(result.cost_usd),
  ].join(' · ')
  const failed = result.failed_sources.map((source) => SOURCE_LABELS[source]).join(', ')
  return (
    <div role="status" className={styles.banner}>
      <span className={styles.outcome}>
        <Icon name="strong" size={OUTCOME_ICON_SIZE} />
        Run finished at {formatClock(result.finished_at)}
      </span>
      <p className={styles.summary}>{counts}</p>
      {failed !== '' && <p className={styles.note}>Could not read {failed}.</p>}
      {result.budget_paused && <p className={styles.note}>AI checks paused at your monthly limit.</p>}
      <Link to={JOBS_PATH} className={`${button.primary} ${button.size36} ${styles.seeNew}`} onClick={onDismiss}>
        See new matches
      </Link>
      <DismissButton onDismiss={onDismiss} />
    </div>
  )
}

export function RunFinishedBanner({ view, onDismiss }: { view: EndedRun; onDismiss: () => void }) {
  if (view.kind === 'finished') return <FinishedRun result={view.result} onDismiss={onDismiss} />
  return (
    <div role="status" className={styles.banner}>
      <p className={styles.summary}>Run cancelled.</p>
      <DismissButton onDismiss={onDismiss} />
    </div>
  )
}
