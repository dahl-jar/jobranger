import { Link } from 'react-router'
import { JOBS_PATH } from '@/app/paths'
import styles from '@/features/run/run.module.css'
import type { EndedRun } from '@/features/run/runView'
import type { RunFinished } from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'
import { formatUsd } from '@/shared/format/money'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import { SOURCE_NAMES } from '@/shared/sources'

const OUTCOME_ICON_SIZE = 16

const COUNT_SEPARATOR = ' · '

function DismissButton({ onDismiss }: { onDismiss: () => void }) {
  const label = useText().run.dismiss
  return (
    <Button variant="link" className={styles.dismiss} aria-label={label} onClick={onDismiss}>
      <Icon name="close" />
    </Button>
  )
}

function runCounts(result: RunFinished, text: Messages): string {
  const words = text.run
  return [words.newAds(result.new_ads), words.matches(result.matches), words.hidden(result.hidden), formatUsd(result.cost_usd, text)].join(COUNT_SEPARATOR)
}

function FinishedRun({ result, onDismiss }: { result: RunFinished; onDismiss: () => void }) {
  const text = useText()
  const words = text.run
  const failed = new Intl.ListFormat(text.numbers.locale, { type: text.numbers.listType }).format(result.failed_sources.map((source) => SOURCE_NAMES[source]))
  return (
    <div role="status" className={styles.banner}>
      <span className={styles.outcome}>
        <Icon name="strong" size={OUTCOME_ICON_SIZE} />
        {words.finishedAt(formatClock(result.finished_at))}
      </span>
      <p className={styles.summary}>{runCounts(result, text)}</p>
      {failed !== '' && <p className={styles.note}>{words.couldNotRead(failed)}</p>}
      {result.budget_paused && <p className={styles.note}>{words.budgetPaused}</p>}
      <Link to={JOBS_PATH} className={`${button.primary} ${button.size36} ${styles.seeNew}`} onClick={onDismiss}>
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
    <div role="status" className={styles.banner}>
      <p className={styles.summary}>{cancelled}</p>
      <DismissButton onDismiss={onDismiss} />
    </div>
  )
}
