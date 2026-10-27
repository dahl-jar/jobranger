import type { Recheck } from '@/features/relay/contract/payload.gen'
import { formatUsd } from '@/shared/format/money'

export function recheckText(recheck: Recheck | undefined): string | undefined {
  if (recheck === undefined || recheck.ads === 0) return undefined
  const count = `Rechecks ${recheck.ads} ${recheck.ads === 1 ? 'ad' : 'ads'} on the next run`
  return recheck.cost_usd === null ? count : `${count}, up to about ${formatUsd(recheck.cost_usd)}`
}
