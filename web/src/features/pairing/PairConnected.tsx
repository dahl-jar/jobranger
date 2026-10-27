import { Link } from 'react-router'
import buttons from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import styles from '@/features/pairing/PairPage.module.css'
import type { Status } from '@/features/relay/contract/payload.gen'

const CONNECTED_ICON_SIZE = 18
const RUN_TIME = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
const APP_HOME = '/app/today'

function lastRunText(status: Status): string {
  if (status.last_run === null) return 'No run yet'
  const matches = status.last_run.matches === 1 ? '1 match' : `${status.last_run.matches} matches`
  return `${RUN_TIME.format(new Date(status.last_run.finished_at))}, ${matches}`
}

interface PairConnectedProps {
  status: Status | undefined
  onUnpair: () => void
}

export function PairConnected({ status, onUnpair }: PairConnectedProps) {
  const facts = status && [
    ['Machine', status.machine],
    ['Version', `jobranger ${status.cli_version}`],
    ['Last run', lastRunText(status)],
  ]

  return (
    <>
      <p role="status" className={styles.connected}>
        <Icon name="strong" size={CONNECTED_ICON_SIZE} />
        Connected
      </p>
      <h1 tabIndex={-1} className={`${styles.title} ${styles.pairedTitle}`}>
        Your computer is paired
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
      <p className={styles.lead}>Roles and places apply right away. Limits and profile apply from the next run.</p>
      <div className={styles.actions}>
        <Link to={APP_HOME} className={`${buttons.primary} ${styles.large}`}>
          Open jobranger
          <Icon name="forward" />
        </Link>
        <button type="button" className={styles.linkButton} onClick={onUnpair}>
          Pair a different machine
        </button>
      </div>
    </>
  )
}
