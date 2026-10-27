import { skipToken, useQuery } from '@tanstack/react-query'
import { type ReactNode, useEffect, useState } from 'react'
import { Navigate } from 'react-router'
import { QUERY_KEYS } from '@/app/queryKeys'
import { useConnection, useRelay } from '@/features/relay/relayContext'
import type { FailReason } from '@/features/relay/sessionRunner'
import { FAILURE_TEXT } from '@/features/shell/failureText'
import { OfflineNotice } from '@/features/shell/OfflineNotice'
import { OfflineScreen } from '@/features/shell/OfflineScreen'
import styles from '@/features/shell/shell.module.css'
import buttons from '@/shared/ui/button.module.css'

const TICK_MS = 1000
const PAIR_PATH = '/pair'

function Waiting({ text }: { text: string }) {
  return (
    <main className={styles.screen}>
      <p role="status" className={styles.waiting}>
        {text}
      </p>
    </main>
  )
}

function RelayDown({ retryAt, onRetry }: { retryAt: number; onRetry: () => void }) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), TICK_MS)
    return () => clearInterval(timer)
  }, [])

  const seconds = Math.max(0, Math.ceil((retryAt - now) / TICK_MS))
  return (
    <main className={styles.screen}>
      <p role="status" className={styles.lead}>
        Can't reach the relay. Trying again in {seconds} s.
      </p>
      <div className={styles.actions}>
        <button type="button" className={buttons.outline} onClick={onRetry}>
          Try now
        </button>
      </div>
    </main>
  )
}

function NetworkOffline() {
  return (
    <main className={styles.screen}>
      <h1 className={styles.title}>You're offline</h1>
      <p className={styles.lead}>This page connects when your network is back.</p>
    </main>
  )
}

function Failed({ reason, onPairAgain }: { reason: FailReason; onPairAgain: () => void }) {
  return (
    <main className={styles.screen}>
      <h1 className={styles.title}>{FAILURE_TEXT[reason]}. Pair again.</h1>
      <div className={styles.actions}>
        <button type="button" className={buttons.primary} onClick={onPairAgain}>
          Pair again
        </button>
      </div>
    </main>
  )
}

export function ConnectionGate({ children }: { children: ReactNode }) {
  const connection = useConnection()
  const { retryNow, unpair } = useRelay()
  const hasList = useQuery({ queryKey: QUERY_KEYS.ads, queryFn: skipToken }).data !== undefined

  switch (connection.kind) {
    case 'unpaired':
      return <Navigate to={PAIR_PATH} replace state={{ reason: connection.reason }} />
    case 'failed':
      return <Failed reason={connection.reason} onPairAgain={() => void unpair()} />
    case 'ready':
      return hasList ? children : <Waiting text="Loading your list..." />
    default:
      break
  }
  if (hasList) {
    return (
      <>
        <OfflineNotice state={connection} />
        {children}
      </>
    )
  }
  if (connection.kind === 'relayDown') return <RelayDown retryAt={connection.retryAt} onRetry={retryNow} />
  if (connection.kind === 'machineOffline') return <OfflineScreen lastSeen={connection.lastSeen} />
  if (connection.kind === 'networkOffline') return <NetworkOffline />
  return <Waiting text="Connecting to your machine..." />
}
