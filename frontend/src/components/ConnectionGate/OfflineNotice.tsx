import type { ConnectionState } from '@/services/relayConnection/connectionStore'
import { formatClock } from '@/helpers/formatDate'
import type { Messages } from '@/i18n/en'
import { useText } from '@/hooks/useLanguage'
import styles from '@/components/ConnectionGate/ConnectionGate.module.css'

function noticeText(state: ConnectionState, text: Messages): string {
  const notices = text.shell.notices
  if (state.kind === 'machineOffline') return state.lastSeen === undefined ? notices.offline : notices.offlineAt(formatClock(state.lastSeen))
  if (state.kind === 'relayDown') return text.connection.relayDown
  if (state.kind === 'networkOffline') return text.connection.networkOffline
  return notices.reconnecting
}

export function OfflineNotice({ state }: { state: ConnectionState }) {
  const text = useText()
  return (
    <p role="status" className={styles.notice}>
      {text.shell.paused(noticeText(state, text))}
    </p>
  )
}
