import styles from '@/features/run/run.module.css'
import type { Status } from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'
import { Icon } from '@/shared/ui/Icon'

function describeRuns(status: Status): string {
  if (status.running !== null) return 'Running now'
  const { last_run: lastRun, next_run_at: nextRunAt } = status
  if (lastRun === null) return nextRunAt === null ? 'No run times set' : `Next run ${formatClock(nextRunAt)}`
  const nextRun = nextRunAt === null ? 'no run times set' : `next run ${formatClock(nextRunAt)}`
  return `Read ${lastRun.ads_read} ads at ${formatClock(lastRun.finished_at)}, ${nextRun}`
}

export function RunLine({ status, offline }: { status: Status | undefined; offline: boolean }) {
  if (offline) {
    return (
      <span className={`${styles.line} ${styles.offline}`}>
        <Icon name="clock" />
        Machine offline
      </span>
    )
  }
  if (status === undefined) return null
  return (
    <span className={styles.line}>
      <Icon name="clock" />
      {describeRuns(status)}
    </span>
  )
}
