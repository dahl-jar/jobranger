import { useState } from 'react'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { QUERY_KEYS } from '@/app/queryKeys'
import { useOptimisticWrite } from '@/app/useOptimisticWrite'
import { markAd } from '@/features/jobs/selectors'
import type { AdMark, AdsSnapshot, AdUnmark, Mark } from '@/features/relay/contract/payload.gen'
import { useClock } from '@/shared/clock'
import { useText } from '@/shared/i18n/languageContext'

type MarkDraft = Omit<AdMark, 'id'> | Omit<AdUnmark, 'id'>

interface MarkState {
  mark: Mark | null
  markedAt: string | null
}

function markOf(snapshot: AdsSnapshot, adId: string): MarkState | undefined {
  const ad = snapshot.active.find((candidate) => candidate.id === adId)
  return ad && { mark: ad.mark, markedAt: ad.marked_at }
}

function sameMark(left: MarkState | undefined, right: MarkState | undefined): boolean {
  return left !== undefined && right !== undefined && left.mark === right.mark && left.markedAt === right.markedAt
}

function withMark(snapshot: AdsSnapshot, adId: string, state: MarkState | undefined): AdsSnapshot {
  return state === undefined ? snapshot : markAd(snapshot, adId, state.mark, state.markedAt)
}

export function useAdMark() {
  const text = useText()
  const clock = useClock()
  const [errors, setErrors] = useState<Readonly<Record<string, string>>>({})

  function setError(adId: string, error: string | undefined) {
    setErrors((current) => {
      const rest = Object.fromEntries(Object.entries(current).filter(([key]) => key !== adId))
      return error === undefined ? rest : { ...rest, [adId]: error }
    })
  }

  const command = useOptimisticWrite<AdsSnapshot, MarkDraft>({
    queryKey: QUERY_KEYS.ads,
    apply: (snapshot, draft) =>
      withMark(snapshot, draft.ad_id, draft.type === 'ad.mark' ? { mark: draft.mark, markedAt: clock().toISOString() } : { mark: null, markedAt: null }),
    holds: (current, draft, written) => sameMark(markOf(current, draft.ad_id), markOf(written, draft.ad_id)),
    restore: (current, draft, previous) => withMark(current, draft.ad_id, markOf(previous, draft.ad_id)),
    onError: (error, draft) => setError(draft.ad_id, commandErrorText(error, text)),
  })

  function send(draft: MarkDraft) {
    setError(draft.ad_id, undefined)
    command.mutate(draft)
  }

  return {
    errors,
    mark: (adId: string, mark: Mark) => send({ type: 'ad.mark', v: 1, ad_id: adId, mark }),
    unmark: (adId: string) => send({ type: 'ad.unmark', v: 1, ad_id: adId }),
  }
}
