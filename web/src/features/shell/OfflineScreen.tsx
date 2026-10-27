import { useState } from 'react'
import { formatClock, formatDayInSentence } from '@/shared/format/dates'
import styles from '@/features/shell/shell.module.css'

export function OfflineScreen({ lastSeen }: { lastSeen: string | undefined }) {
  const [now] = useState(() => new Date().toISOString())
  return (
    <main className={styles.screen}>
      <h1 className={styles.title}>Your machine is offline</h1>
      {lastSeen !== undefined && (
        <p className={styles.lead}>
          Last seen {formatDayInSentence(lastSeen, now)} at {formatClock(lastSeen)}.
        </p>
      )}
      <p className={styles.lead}>
        Run <code className={styles.command}>jobranger serve</code> on it. This page connects when it comes online.
      </p>
    </main>
  )
}
