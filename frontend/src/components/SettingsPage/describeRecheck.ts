import type { Recheck } from '@/contract/payload.gen'
import { formatUsd } from '@/helpers/formatUsd'
import type { Messages } from '@/i18n/en'

export function describeRecheck(recheck: Recheck | undefined, text: Messages): string | undefined {
  if (recheck === undefined || recheck.ads === 0) return undefined
  const count = text.settings.recheck(recheck.ads)
  return recheck.cost_usd === null ? count : text.settings.recheckCost(count, formatUsd(recheck.cost_usd, text))
}
