import { skipToken, useQuery } from '@tanstack/react-query'
import { useEffect, useEffectEvent } from 'react'
import { QUERY_KEYS } from '@/app/queryKeys'
import { PairConnected } from '@/features/pairing/PairConnected'
import type { UnpairReason } from '@/features/relay/connectionStore'
import type { Status } from '@/features/relay/contract/payload.gen'
import { useConnection, useRelay } from '@/features/relay/relayContext'
import { RelayProvider } from '@/features/relay/RelayProvider'
import { connectionText } from '@/features/shell/connectionText'
import { useText } from '@/shared/i18n/languageContext'

interface PairedSessionProps {
  onEnded: (reason: UnpairReason | undefined) => void
}

function PairedView({ onEnded }: PairedSessionProps) {
  const connection = useConnection()
  const relay = useRelay()
  const status = useQuery<Status>({ queryKey: QUERY_KEYS.status, queryFn: skipToken }).data
  const notifyEnded = useEffectEvent(onEnded)
  const text = useText()

  useEffect(() => {
    if (connection.kind === 'unpaired') notifyEnded(connection.reason)
  }, [connection])

  return (
    <PairConnected
      status={status}
      connection={connectionText(connection, text)}
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
