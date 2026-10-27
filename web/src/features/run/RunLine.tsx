import styles from '@/features/run/run.module.css'
import type { Status } from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

function describeRuns(status: Status, text: Messages): string {
  const words = text.run
  if (status.running !== null) return words.runningNow
  const { last_run: lastRun, next_run_at: nextRunAt } = status
  if (lastRun === null) return nextRunAt === null ? words.noTimes : words.nextRun(formatClock(nextRunAt))
  const nextRun = nextRunAt === null ? words.noTimesInSentence : words.nextRunInSentence(formatClock(nextRunAt))
  return words.lastRun(lastRun.ads_read, formatClock(lastRun.finished_at), nextRun)
}

export function RunLine({ status, offline }: { status: Status | undefined; offline: boolean }) {
  const text = useText()
  if (offline) {
    return (
      <span className={`${styles.line} ${styles.offline}`}>
        <Icon name="clock" />
        {text.run.offline}
      </span>
    )
  }
  if (status === undefined) return null
  return (
    <span className={styles.line}>
      <Icon name="clock" />
      {describeRuns(status, text)}
    </span>
  )
}
