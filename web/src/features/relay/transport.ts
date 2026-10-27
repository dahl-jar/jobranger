import type { Envelope } from '@/features/relay/contract/envelope.gen'

export interface Presence {
  cli_online: boolean
}

export interface RelayEvents {
  onEnvelope(text: string): void
  onPresence(presence: Presence): void
  onClose(): void
}

export interface RelayTransport {
  send(envelope: Envelope): Promise<void>
  stop(): Promise<void>
}

export type ConnectRelay = (handle: string, events: RelayEvents) => Promise<RelayTransport>
