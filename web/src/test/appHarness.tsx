import { QueryClientProvider } from '@tanstack/react-query'
import { configure, render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { StrictMode } from 'react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'
import { appChildren } from '@/app/appRoutes'
import { createQueryClient } from '@/app/queryClient'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import { seedSnapshots, type Snapshots } from '@/app/useSnapshot'
import { type ConnectionState, createConnectionStore } from '@/features/relay/connectionStore'
import { type Relay, RelayContext } from '@/features/relay/relayContext'
import { PairedShell } from '@/features/shell/AppLayout'
import { LanguageProvider } from '@/shared/i18n/LanguageProvider'
import { SAVED, type SendMock, SNAPSHOTS } from '@/test/appData'
import { warmRoutes } from '@/test/warmRoutes'

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
