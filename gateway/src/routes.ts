const GROUPS = ['pair', 'session', 'hub'] as const

export type RouteGroup = (typeof GROUPS)[number]

export function groupFor(url: string): RouteGroup | undefined {
  const { pathname } = new URL(url)
  return GROUPS.find((group) => pathname === `/${group}` || pathname.startsWith(`/${group}/`))
}
