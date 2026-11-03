import { QueryClientProvider } from '@tanstack/react-query'
import { configure, render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { StrictMode } from 'react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'
import { appChildren } from '@/components/App/appRoutes'
import { createQueryClient } from '@/components/App/createQueryClient'
import { ThemeProvider } from '@/components/ThemeProvider/ThemeProvider'
import { seedSnapshots, type Snapshots } from '@/hooks/useSnapshot'
import { type ConnectionState, createConnectionStore } from '@/services/relayConnection/connectionStore'
import { type Relay, RelayContext } from '@/hooks/useRelay'
import { PairedShell } from '@/components/AppLayout/AppLayout'
import { LanguageProvider } from '@/components/LanguageProvider/LanguageProvider'
import { SAVED, type SendMock, SNAPSHOTS } from '@/__tests__/appPayloadMocks'
import { warmRoutes } from '@/__tests__/warmRoutes'

interface AppOptions {
  state?: ConnectionState
  data?: Partial<Snapshots>
  send?: SendMock
}


const APP_RENDER_WAIT_MS = 3000

configure({ asyncUtilTimeout: APP_RENDER_WAIT_MS })
warmRoutes(appChildren)

export function renderApp(path: string, { state = { kind: 'ready' }, data = SNAPSHOTS, send = vi.fn(async () => SAVED) }: AppOptions = {}) {
  const store = createConnectionStore()
  store.dispatch(state)
  const unpair = vi.fn(async () => undefined)
  const relay: Relay = { store, send, unpair, retryNow: vi.fn() }
  const queryClient = createQueryClient()
  seedSnapshots(queryClient, data)
  const router = createMemoryRouter([{ path: '/app', element: <PairedShell />, children: appChildren }], { initialEntries: [path] })
  render(
    <StrictMode>
      <LanguageProvider>
        <ThemeProvider>
          <QueryClientProvider client={queryClient}>
            <RelayContext value={relay}>
              <RouterProvider router={router} />
            </RelayContext>
          </QueryClientProvider>
        </ThemeProvider>
      </LanguageProvider>
    </StrictMode>,
  )
  return { queryClient, store, send, unpair, router, user: userEvent.setup() }
}
