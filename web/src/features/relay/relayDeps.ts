import { agentName } from '@/features/pairing/agentName'
import { fetchHandle } from '@/features/relay/handleClient'
import { connectHub } from '@/features/relay/hubTransport'
import { clearPairingOf, loadPairing } from '@/features/relay/keyStore'
import { createWorkerParser } from '@/features/relay/payloadParser'
import type { RelayDeps } from '@/features/relay/relayContext'
import { retryDelayMs } from '@/features/relay/retryPolicy'

export function defaultRelayDeps(): RelayDeps {
  return {
    loadPairing,
    clearPairingOf,
    fetchHandle,
    connect: connectHub(),
    agent: agentName(navigator.userAgent),
    retryDelay: retryDelayMs,
    parseAssembled: createWorkerParser(() => new Worker(new URL('./payloadWorker.ts', import.meta.url), { type: 'module' })),
  }
}
