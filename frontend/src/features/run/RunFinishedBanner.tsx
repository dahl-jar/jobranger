import { Link } from 'react-router'
import { jobsPath, useAppBase } from '@/app/paths'
import styles from '@/features/run/run.module.css'
import { type EndedRun, runCounts } from '@/features/run/runView'
import type { RunFinished } from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import { SOURCE_NAMES } from '@/shared/labels/sources'

const OUTCOME_ICON_SIZE = 16

function DismissButton({ onDismiss }: { onDismiss: () => void }) {
  const label = useText().run.dismiss
  return (
    <Button variant="link" className={styles.dismiss} aria-label={label} onClick={onDismiss}>
      <Icon name="close" />
    </Button>
  )
}

function FinishedRun({ result, onDismiss }: { result: RunFinished; onDismiss: () => void }) {
  const text = useText()
  const base = useAppBase()
  const words = text.run
  const failed = new Intl.ListFormat(text.numbers.locale, { type: text.numbers.listType }).format(result.failed_sources.map((source) => SOURCE_NAMES[source]))
  return (
    <div className={styles.banner}>
      <span className={styles.outcome}>
        <Icon name="strong" size={OUTCOME_ICON_SIZE} />
        {words.finishedAt(formatClock(result.finished_at))}
      </span>
      <p className={styles.summary}>{runCounts(result, text)}</p>
      {failed !== '' && <p className={styles.note}>{words.couldNotRead(failed)}</p>}
      {result.budget_paused && <p className={styles.note}>{words.budgetPaused}</p>}
      <Link to={jobsPath(base)} className={`${button.primary} ${button.size36} ${styles.seeNew}`} onClick={onDismiss}>
        {words.seeNew}
      </Link>
      <DismissButton onDismiss={onDismiss} />
    </div>
  )
}

export function RunFinishedBanner({ view, onDismiss }: { view: EndedRun; onDismiss: () => void }) {
  const cancelled = useText().run.cancelled
  if (view.kind === 'finished') return <FinishedRun result={view.result} onDismiss={onDismiss} />
  return (
    <div className={styles.banner}>
      <p className={styles.summary}>{cancelled}</p>
      <DismissButton onDismiss={onDismiss} />
    </div>
  )
}
