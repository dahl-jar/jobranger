import type { RouteObject } from 'react-router'

export async function preloadLazyRoutes(routes: RouteObject[]): Promise<void> {
  await Promise.all(
    routes.map(async (route) => {
      if (typeof route.lazy === 'function') await route.lazy()
      await preloadLazyRoutes(route.children ?? [])
    }),
  )
}
