import type { ConnectionState } from '@/services/relayConnection/connectionStore'
import type { Messages } from '@/i18n/en'

export function describeConnection(connection: ConnectionState, text: Messages): string | undefined {
  const words = text.connection
  switch (connection.kind) {
    case 'ready':
      return undefined
    case 'failed':
      return words.failures[connection.reason]
    case 'machineOffline':
      return words.machineOffline
    case 'relayDown':
      return words.relayDown
    case 'networkOffline':
      return words.networkOffline
    default:
      return words.connecting
  }
}
