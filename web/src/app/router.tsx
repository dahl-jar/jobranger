import type { RouteObject } from 'react-router'
import { NotFound } from '@/app/NotFound'
import { PageLoading } from '@/app/PageLoading'
import { PublicLayout, type PublicLayoutHandle } from '@/app/PublicLayout'
import { RouteError } from '@/app/RouteError'
import { routeImport } from '@/app/routeImport'
import { LandingNav, OpenAppLink } from '@/features/landing/LandingHeader'
import { LandingPage } from '@/features/landing/LandingPage'
import { SetupSubtitle } from '@/features/setup/SetupSubtitle'

export const routes: RouteObject[] = [
  {
    ErrorBoundary: RouteError,
    children: [
      {
        Component: PublicLayout,
        children: [
          {
            index: true,
            Component: LandingPage,
            handle: { nav: <LandingNav />, action: <OpenAppLink /> } satisfies PublicLayoutHandle,
          },
          {
            path: 'setup',
            lazy: routeImport(async () => ({ Component: (await import('@/features/setup/SetupPage')).SetupPage })),
            HydrateFallback: PageLoading,
            handle: { subtitle: <SetupSubtitle />, showHome: true, wide: true } satisfies PublicLayoutHandle,
          },
          {
            path: 'pair',
            lazy: async () => ({ Component: (await import('@/features/pairing/PairPage')).PairPage }),
            HydrateFallback: PageLoading,
            handle: { showHome: true } satisfies PublicLayoutHandle,
          },
          { path: '*', Component: NotFound },
        ],
      },
    ],
  },
]
