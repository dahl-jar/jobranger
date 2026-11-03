import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, useContext, useEffect, useEffectEvent, useRef, useState } from 'react'
import { CommandTracker, disconnectedError } from '@/services/relayCommands/commandTracker'
import { createConnectionStore } from '@/services/relayConnection/connectionStore'
import type { Payload } from '@/contract/payload.gen'
import { routeIncoming } from '@/components/RelayProvider/routeIncoming'
import { RelayClient } from '@/services/relaySession/relayClient'
import { type Relay, RelayContext, RelayDepsContext } from '@/hooks/useRelay'
import { defaultRelayDeps } from '@/components/RelayProvider/defaultRelayDeps'

interface Live {
  client: RelayClient
  commands: CommandTracker
}

export function RelayProvider({ children }: { children: ReactNode }) {
  const injected = useContext(RelayDepsContext)
  const queryClient = useQueryClient()
  const [store] = useState(createConnectionStore)
  const live = useRef<Live | undefined>(undefined)
  const [relay] = useState<Relay>(() => ({
    store,
    send: (draft, onProgress) => live.current?.commands.send(draft, onProgress) ?? Promise.reject(disconnectedError()),
    unpair: async () => {
      await live.current?.client.unpair()
    },
    retryNow: () => live.current?.client.retryNow(),
  }))

  const onMessage = useEffectEvent((message: Payload, current: Live) => {
    routeIncoming(message, queryClient, {
      commands: current.commands,
      onUnpaired: (reason) => void current.client.unpair(reason),
    })
  })

  useEffect(() => {
    const deps = injected ?? defaultRelayDeps()
    const commands = new CommandTracker((command, onProgress) => client.send(command, onProgress))
    const client: RelayClient = new RelayClient({
      ...deps,
      onState: (state) => {
        if (state.kind === 'unpaired') queryClient.clear()
        store.dispatch(state)
      },
      onMessage: (message) => onMessage(message, current),
      onDisconnect: () => commands.failAll(),
      onSessionReset: () => commands.hold(),
      onReady: () => commands.resendHeld(),
    })
    const current: Live = { client, commands }
    live.current = current
    const onVisible = () => {
      if (document.visibilityState === 'visible') client.retryNow()
    }
    const onOffline = () => client.pauseForNetwork()
    const onOnline = () => client.resumeForNetwork()
    document.addEventListener('visibilitychange', onVisible)
    window.addEventListener('offline', onOffline)
    window.addEventListener('online', onOnline)
    if (!navigator.onLine) client.pauseForNetwork()
    void client.start()
    return () => {
      document.removeEventListener('visibilitychange', onVisible)
      window.removeEventListener('offline', onOffline)
      window.removeEventListener('online', onOnline)
      live.current = undefined
      commands.failAll()
      void client.stop()
    }
  }, [injected, store, queryClient])

  return <RelayContext value={relay}>{children}</RelayContext>
}
