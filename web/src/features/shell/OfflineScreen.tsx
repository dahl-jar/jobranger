import { useState } from 'react'
import { formatClock, formatDayInSentence } from '@/shared/format/dates'
import { useText } from '@/shared/i18n/languageContext'
import styles from '@/features/shell/shell.module.css'

export function OfflineScreen({ lastSeen }: { lastSeen: string | undefined }) {
  const [now] = useState(() => new Date().toISOString())
  const text = useText()
  const words = text.shell
  return (
    <main className={styles.screen}>
      <h1 className={styles.title}>{text.connection.machineOffline}</h1>
      {lastSeen !== undefined && <p className={styles.lead}>{words.lastSeen(formatDayInSentence(lastSeen, now, text), formatClock(lastSeen))}</p>}
      <p className={styles.lead}>
        {words.runServeBefore}
        <code className={styles.command}>jobranger serve</code>
        {words.runServeAfter}
      </p>
    </main>
  )
}
