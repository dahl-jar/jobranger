import { createElement, type ReactNode } from 'react'
import { useLocation } from 'react-router'
import { renderRoutes } from '@/test/render'

const DETAIL_ROUTE = '/app/ad'

function OpenedFrom() {
  const { state } = useLocation()
  return createElement('p', null, `opened from ${String(Reflect.get(Object(state), 'from'))}`)
}

export function renderListRoute(path: string, element: ReactNode) {
  return renderRoutes(
    [
      { path, element },
      { path: DETAIL_ROUTE, Component: OpenedFrom },
    ],
    path,
  )
}
