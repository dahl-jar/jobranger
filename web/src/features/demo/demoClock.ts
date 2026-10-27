import { DEMO_TODAY } from '@/features/demo/demoSnapshots'
import type { Clock } from '@/shared/clock'

const DEMO_OPENED_AT = Date.parse(`${DEMO_TODAY}T12:00:00Z`)

export function demoClock(openedAt: number = Date.now()): Clock {
  return () => new Date(DEMO_OPENED_AT + Date.now() - openedAt)
}
