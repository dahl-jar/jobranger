import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { RawReceiveProtocol } from '@/features/relay/rawReceiveProtocol'
import type { ConnectRelay, Presence } from '@/features/relay/transport'
import { asRecord } from '@/shared/asRecord'
import { RELAY_URL } from '@/shared/config'

const SERVER_TIMEOUT_MS = 30_000
const KEEP_ALIVE_MS = 15_000

function isPresence(value: unknown): value is Presence {
  const fields = asRecord(value)
  return typeof fields?.cli_online === 'boolean' && Number.isSafeInteger(fields.browsers)
}

export function connectHub(relayUrl = RELAY_URL): ConnectRelay {
  return async (handle, events) => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${relayUrl}/hub`, {
        accessTokenFactory: () => handle,
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withHubProtocol(new RawReceiveProtocol())
      .withServerTimeout(SERVER_TIMEOUT_MS)
      .withKeepAliveInterval(KEEP_ALIVE_MS)
      .configureLogging(LogLevel.None)
      .build()
    connection.on('Receive', (envelope: string) => events.onEnvelope(envelope))
    connection.on('Presence', (presence: unknown) => {
      if (isPresence(presence)) events.onPresence({ cli_online: presence.cli_online })
    })
    connection.onclose(() => events.onClose())
    await connection.start()
    return {
      send: (envelope) => connection.invoke('Send', envelope),
      stop: () => connection.stop(),
    }
  }
}
