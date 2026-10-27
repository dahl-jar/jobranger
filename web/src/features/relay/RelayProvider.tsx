import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, useContext, useEffect, useEffectEvent, useRef, useState } from 'react'
import { CommandTracker, disconnectedError } from '@/features/relay/commands'
import { createConnectionStore } from '@/features/relay/connectionStore'
import type { Payload } from '@/features/relay/contract/payload.gen'
import { routeIncoming } from '@/features/relay/messageRouter'
import { RelayClient } from '@/features/relay/relayClient'
import { type Relay, RelayContext, RelayDepsContext } from '@/features/relay/relayContext'
import { defaultRelayDeps } from '@/features/relay/relayDeps'

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
    send: (draft) => live.current?.commands.send(draft) ?? Promise.reject(disconnectedError()),
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
    const commands = new CommandTracker((command) => client.send(command))
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
