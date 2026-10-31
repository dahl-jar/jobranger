import { skipToken, useQuery } from '@tanstack/react-query'
import { type ReactNode, useEffect, useState } from 'react'
import { Navigate } from 'react-router'
import { QUERY_KEYS } from '@/app/queryKeys'
import { useConnection, useRelay } from '@/features/relay/relayContext'
import type { FailReason } from '@/features/relay/sessionRunner'
import { OfflineNotice } from '@/features/shell/OfflineNotice'
import { OfflineScreen } from '@/features/shell/OfflineScreen'
import styles from '@/features/shell/shell.module.css'
import buttons from '@/shared/ui/button.module.css'
import { useText } from '@/shared/i18n/languageContext'

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

  const words = useText().shell
  const seconds = Math.max(0, Math.ceil((retryAt - now) / TICK_MS))
  return (
    <main className={styles.screen}>
      <p role="status" className={styles.lead}>
        {words.retryIn(seconds)}
      </p>
      <div className={styles.actions}>
        <button type="button" className={buttons.outline} onClick={onRetry}>
          {words.tryNow}
        </button>
      </div>
    </main>
  )
}

function NetworkOffline() {
  const text = useText()
  return (
    <main className={styles.screen}>
      <h1 className={styles.title}>{text.connection.networkOffline}</h1>
      <p className={styles.lead}>{text.shell.networkBack}</p>
    </main>
  )
}

function Failed({ reason, onPairAgain }: { reason: FailReason; onPairAgain: () => void }) {
  const words = useText().connection
  return (
    <main className={styles.screen}>
      <h1 className={styles.title}>{words.pairAgainTitle(words.failures[reason])}</h1>
      <div className={styles.actions}>
        <button type="button" className={buttons.primary} onClick={onPairAgain}>
          {words.pairAgain}
        </button>
      </div>
    </main>
  )
}

export function ConnectionGate({ children }: { children: ReactNode }) {
  const connection = useConnection()
  const { retryNow, unpair } = useRelay()
  const hasList = useQuery({ queryKey: QUERY_KEYS.ads, queryFn: skipToken }).data !== undefined
  const text = useText()

  if (connection.kind === 'unpaired') return <Navigate to={PAIR_PATH} replace state={{ reason: connection.reason }} />
  if (connection.kind === 'failed') return <Failed reason={connection.reason} onPairAgain={() => void unpair()} />
  if (hasList) {
    return (
      <>
        {connection.kind === 'ready' ? null : <OfflineNotice state={connection} />}
        {children}
      </>
    )
  }
  if (connection.kind === 'ready') return <Waiting text={text.shell.loadingList} />
  if (connection.kind === 'relayDown') return <RelayDown retryAt={connection.retryAt} onRetry={retryNow} />
  if (connection.kind === 'machineOffline') return <OfflineScreen lastSeen={connection.lastSeen} />
  if (connection.kind === 'networkOffline') return <NetworkOffline />
  return <Waiting text={text.connection.connecting} />
}
