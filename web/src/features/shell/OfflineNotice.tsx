import type { ConnectionState } from '@/features/relay/connectionStore'
import { formatClock } from '@/shared/format/dates'
import styles from '@/features/shell/shell.module.css'

const PAUSED = 'Changes are paused.'

function noticeText(state: ConnectionState): string {
  if (state.kind === 'machineOffline') {
    const when = state.lastSeen === undefined ? '' : ` at ${formatClock(state.lastSeen)}`
    return `Your machine went offline${when}. ${PAUSED}`
  }
  if (state.kind === 'relayDown') return `Can't reach the relay. ${PAUSED}`
  if (state.kind === 'networkOffline') return `You're offline. ${PAUSED}`
  return `Reconnecting to your machine. ${PAUSED}`
}

export function OfflineNotice({ state }: { state: ConnectionState }) {
  return (
    <p role="status" className={styles.notice}>
      {noticeText(state)}
    </p>
  )
}
