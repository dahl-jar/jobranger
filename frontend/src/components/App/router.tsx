import type { RouteObject } from 'react-router'
import { appChildren } from '@/components/App/appRoutes'
import { NotFound } from '@/components/NotFound/NotFound'
import { PageLoading } from '@/components/PageLoading/PageLoading'
import { PublicLayout, type PublicLayoutHandle } from '@/components/PublicLayout/PublicLayout'
import { RouteError } from '@/components/RouteError/RouteError'
import { routeImport } from '@/helpers/routeImport'
import { LandingActions, LandingNav } from '@/components/LandingPage/LandingHeader'
import { LandingPage } from '@/components/LandingPage/LandingPage'
import { SetupSubtitle } from '@/components/SetupPage/SetupSubtitle'

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
            handle: { nav: <LandingNav />, action: <LandingActions /> } satisfies PublicLayoutHandle,
          },
          {
            path: 'setup',
            lazy: routeImport(async () => ({ Component: (await import('@/components/SetupPage/SetupPage')).SetupPage })),
            HydrateFallback: PageLoading,
            handle: { subtitle: <SetupSubtitle />, showHome: true, wide: true } satisfies PublicLayoutHandle,
          },
          {
            path: 'pair',
            lazy: async () => ({ Component: (await import('@/components/PairPage/PairPage')).PairPage }),
            HydrateFallback: PageLoading,
            handle: { showHome: true } satisfies PublicLayoutHandle,
          },
          { path: '*', Component: NotFound },
        ],
      },
      {
        path: 'app',
        lazy: routeImport(async () => ({ Component: (await import('@/components/AppLayout/AppLayout')).AppLayout })),
        HydrateFallback: PageLoading,
        children: appChildren,
      },
      {
        path: 'demo',
        lazy: routeImport(async () => ({ Component: (await import('@/components/DemoLayout/DemoLayout')).DemoLayout })),
        HydrateFallback: PageLoading,
        children: appChildren,
      },
    ],
  },
]
