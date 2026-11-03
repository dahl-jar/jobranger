import { Navigate, type RouteObject } from 'react-router'
import { NotFound } from '@/components/NotFound/NotFound'
import { PageLoading } from '@/components/PageLoading/PageLoading'
import { routeImport } from '@/helpers/routeImport'
import { DEFAULT_LIST } from '@/helpers/adSelectors'
import { DEFAULT_SECTION } from '@/constants/settingsSections'

export const appChildren: RouteObject[] = [
  { index: true, element: <Navigate to={DEFAULT_LIST} replace /> },
  {
    lazy: routeImport(async () => ({ Component: (await import('@/components/JobsLayout/JobsLayout')).JobsLayout })),
    HydrateFallback: PageLoading,
    children: [
      {
        path: 'ad',
        lazy: routeImport(async () => ({ Component: (await import('@/components/AdDetailPage/AdDetailPage')).AdDetailPage })),
        HydrateFallback: PageLoading,
      },
      {
        path: 'applications',
        lazy: routeImport(async () => ({ Component: (await import('@/components/ApplicationsScreen/ApplicationsScreen')).ApplicationsScreen })),
        HydrateFallback: PageLoading,
        children: [{ path: ':applicationId', element: null }],
      },
      {
        path: ':list',
        lazy: routeImport(async () => ({ Component: (await import('@/components/JobsPage/JobsPage')).JobsPage })),
        HydrateFallback: PageLoading,
      },
    ],
  },
  { path: 'settings', element: <Navigate to={DEFAULT_SECTION} replace /> },
  {
    path: 'settings/:section',
    lazy: routeImport(async () => ({ Component: (await import('@/components/SettingsPage/SettingsPage')).SettingsPage })),
    HydrateFallback: PageLoading,
  },
  { path: '*', Component: NotFound },
]
