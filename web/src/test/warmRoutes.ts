import type { RouteObject } from 'react-router'
import { beforeAll } from 'vitest'

function routeLoads(routes: readonly RouteObject[]): Promise<unknown>[] {
  return routes.flatMap((route) => [...(typeof route.lazy === 'function' ? [route.lazy()] : []), ...routeLoads(route.children ?? [])])
}

export function warmRoutes(routes: readonly RouteObject[]): void {
  beforeAll(async () => {
    await Promise.all(routeLoads(routes))
  })
}
