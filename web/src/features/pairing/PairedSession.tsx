import { skipToken, useQuery } from '@tanstack/react-query'
import { useEffect, useEffectEvent } from 'react'
import { QUERY_KEYS } from '@/app/queryKeys'
import { PairConnected } from '@/features/pairing/PairConnected'
import type { ConnectionState, UnpairReason } from '@/features/relay/connectionStore'
import type { Status } from '@/features/relay/contract/payload.gen'
import { useConnection, useRelay } from '@/features/relay/relayContext'
import { RelayProvider } from '@/features/relay/RelayProvider'
import { FAILURE_TEXT } from '@/features/shell/failureText'

const CONNECTING = 'Connecting to your machine...'

const CONNECTION_TEXT: Record<Exclude<ConnectionState['kind'], 'ready' | 'failed'>, string> = {
  loading: CONNECTING,
  unpaired: CONNECTING,
  connecting: CONNECTING,
  handshaking: CONNECTING,
  machineOffline: 'Your machine is offline',
  relayDown: "Can't reach the relay",
  networkOffline: "You're offline",
}

function connectionText(connection: ConnectionState): string | undefined {
  if (connection.kind === 'ready') return undefined
  return connection.kind === 'failed' ? FAILURE_TEXT[connection.reason] : CONNECTION_TEXT[connection.kind]
}

interface PairedSessionProps {
  onEnded: (reason: UnpairReason | undefined) => void
}

function PairedView({ onEnded }: PairedSessionProps) {
  const connection = useConnection()
  const relay = useRelay()
  const status = useQuery<Status>({ queryKey: QUERY_KEYS.status, queryFn: skipToken }).data
  const notifyEnded = useEffectEvent(onEnded)

  useEffect(() => {
    if (connection.kind === 'unpaired') notifyEnded(connection.reason)
  }, [connection])

  return (
    <PairConnected
      status={status}
      connection={connectionText(connection)}
      onUnpair={() => void relay.unpair()}
    />
  )
}

export function PairedSession({ onEnded }: PairedSessionProps) {
  return (
    <RelayProvider>
      <PairedView onEnded={onEnded} />
    </RelayProvider>
  )
}
