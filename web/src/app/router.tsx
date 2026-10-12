import type { RouteObject } from 'react-router'
import { NotFound } from '@/app/NotFound'
import { PublicLayout } from '@/app/PublicLayout'
import { RouteError } from '@/app/RouteError'

export const routes: RouteObject[] = [
  {
    ErrorBoundary: RouteError,
    children: [
      {
        Component: PublicLayout,
        children: [{ path: '*', Component: NotFound }],
      },
    ],
  },
]
