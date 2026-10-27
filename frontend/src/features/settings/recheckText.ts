import type { Recheck } from '@/features/relay/contract/payload.gen'
import { formatUsd } from '@/shared/format/money'
import { en, type Messages } from '@/shared/i18n/en'

export function recheckText(recheck: Recheck | undefined, text: Messages = en): string | undefined {
  if (recheck === undefined || recheck.ads === 0) return undefined
  const count = text.settings.recheck(recheck.ads)
  return recheck.cost_usd === null ? count : text.settings.recheckCost(count, formatUsd(recheck.cost_usd, text))
}
