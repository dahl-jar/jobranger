import { Link } from 'react-router'
import { APP_BASE, jobsPath } from '@/app/paths'
import buttons from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import styles from '@/features/pairing/PairPage.module.css'
import type { Status } from '@/features/relay/contract/payload.gen'
import { formatClock, formatDate } from '@/shared/format/dates'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'

const CONNECTED_ICON_SIZE = 18

function lastRunText(status: Status, text: Messages): string {
  const words = text.pairing
  if (status.last_run === null) return words.noRun
  const finishedAt = status.last_run.finished_at
  return words.runSummary(formatDate(finishedAt, text), formatClock(finishedAt), status.last_run.matches)
}

interface PairConnectedProps {
  status: Status | undefined
  connection?: string | undefined
  onUnpair: () => void
}

export function PairConnected({ status, connection, onUnpair }: PairConnectedProps) {
  const text = useText()
  const words = text.pairing
  const facts = status && [
    [words.machine, status.machine],
    [words.version, text.settings.connection.version(status.cli_version)],
    [words.lastRun, lastRunText(status, text)],
  ]

  return (
    <>
      {connection === undefined ? (
        <p role="status" className={styles.connected}>
          <Icon name="strong" size={CONNECTED_ICON_SIZE} />
          {words.connected}
        </p>
      ) : (
        <p role="status" className={styles.lead}>
          {connection}
        </p>
      )}
      <h1 tabIndex={-1} className={`${styles.title} ${styles.pairedTitle}`}>
        {words.pairedTitle}
      </h1>
      {facts && (
        <dl className={styles.facts}>
          {facts.map(([label, value]) => (
            <div key={label} className={styles.fact}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      )}
      <p className={styles.lead}>{words.applies}</p>
      <div className={styles.actions}>
        <Link to={jobsPath(APP_BASE)} className={`${buttons.primary} ${styles.large}`}>
          {words.open}
          <Icon name="forward" />
        </Link>
        <button type="button" className={styles.linkButton} onClick={onUnpair}>
          {words.pairDifferent}
        </button>
      </div>
    </>
  )
}
