import { Navigate, type RouteObject } from 'react-router'
import { NotFound } from '@/app/NotFound'
import { PageLoading } from '@/app/PageLoading'
import { routeImport } from '@/app/routeImport'
import { DEFAULT_LIST } from '@/features/jobs/selectors'
import { DEFAULT_SECTION } from '@/features/settings/settingsSections'

export const appChildren: RouteObject[] = [
  { index: true, element: <Navigate to={DEFAULT_LIST} replace /> },
  {
    lazy: routeImport(async () => ({ Component: (await import('@/features/jobs/JobsLayout')).JobsLayout })),
    HydrateFallback: PageLoading,
    children: [
      {
        path: 'ad',
        lazy: routeImport(async () => ({ Component: (await import('@/features/jobs/AdDetailPage')).AdDetailPage })),
        HydrateFallback: PageLoading,
      },
      {
        path: 'applications',
        lazy: routeImport(async () => ({ Component: (await import('@/features/applications/ApplicationsScreen')).ApplicationsScreen })),
        HydrateFallback: PageLoading,
        children: [{ path: ':applicationId', element: null }],
      },
      {
        path: ':list',
        lazy: routeImport(async () => ({ Component: (await import('@/features/jobs/JobsPage')).JobsPage })),
        HydrateFallback: PageLoading,
      },
    ],
  },
  { path: 'settings', element: <Navigate to={DEFAULT_SECTION} replace /> },
  {
    path: 'settings/:section',
    lazy: routeImport(async () => ({ Component: (await import('@/features/settings/SettingsPage')).SettingsPage })),
    HydrateFallback: PageLoading,
  },
  { path: '*', Component: NotFound },
]
