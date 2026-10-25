import type { RouteObject } from 'react-router'
import { NotFound } from '@/app/NotFound'
import { PageLoading } from '@/app/PageLoading'
import { PublicLayout, type PublicLayoutHandle } from '@/app/PublicLayout'
import { RouteError } from '@/app/RouteError'
import { LandingNav, OpenAppLink } from '@/features/landing/LandingHeader'
import { LandingPage } from '@/features/landing/LandingPage'

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
            lazy: async () => ({ Component: (await import('@/features/setup/SetupPage')).SetupPage }),
            HydrateFallback: PageLoading,
            handle: { subtitle: 'Setup walkthrough', showHome: true, wide: true } satisfies PublicLayoutHandle,
          },
          { path: '*', Component: NotFound },
        ],
      },
    ],
  },
]
