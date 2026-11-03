import { agentName } from '@/helpers/agentName'
import { fetchHandle } from '@/services/relayTransport/fetchHandle'
import { connectHub } from '@/services/relayTransport/connectHub'
import { clearPairingOf, loadPairing } from '@/services/pairingStore/pairingStorage'
import { createWorkerParser } from '@/services/relayMessages/workerPayloadParser'
import type { RelayDeps } from '@/hooks/useRelay'
import { retryDelayMs } from '@/services/relayTransport/retryDelay'

export function defaultRelayDeps(): RelayDeps {
  return {
    loadPairing,
    clearPairingOf,
    fetchHandle,
    connect: connectHub(),
    agent: agentName(navigator.userAgent),
    retryDelay: retryDelayMs,
    parseAssembled: createWorkerParser(() => new Worker(new URL('../../services/relayMessages/payloadParseWorker.ts', import.meta.url), { type: 'module' })),
  }
}
