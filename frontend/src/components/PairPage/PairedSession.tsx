import { skipToken, useQuery } from '@tanstack/react-query'
import { useEffect, useEffectEvent } from 'react'
import { QUERY_KEYS } from '@/constants/queryKeys'
import { PairConnected } from '@/components/PairPage/PairConnected'
import type { UnpairReason } from '@/services/relayConnection/connectionStore'
import type { Status } from '@/contract/payload.gen'
import { useConnection, useRelay } from '@/hooks/useRelay'
import { RelayProvider } from '@/components/RelayProvider/RelayProvider'
import { describeConnection } from '@/components/PairPage/describeConnection'
import { useText } from '@/hooks/useLanguage'

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
      connection={describeConnection(connection, text)}
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
