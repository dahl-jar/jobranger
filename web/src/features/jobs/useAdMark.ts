import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { QUERY_KEYS } from '@/app/queryKeys'
import { markAd } from '@/features/jobs/selectors'
import type { AdMark, AdsSnapshot, AdUnmark, Mark } from '@/features/relay/contract/payload.gen'
import { useCommand } from '@/features/relay/useCommand'
import { useClock } from '@/shared/clock'
import { useText } from '@/shared/i18n/languageContext'

type MarkDraft = Omit<AdMark, 'id'> | Omit<AdUnmark, 'id'>

interface MarkState {
  mark: Mark | null
  markedAt: string | null
}

interface MarkContext {
  previous: MarkState
  written: MarkState
}

function markOf(snapshot: AdsSnapshot | undefined, adId: string): MarkState | undefined {
  const ad = snapshot?.active.find((candidate) => candidate.id === adId)
  return ad && { mark: ad.mark, markedAt: ad.marked_at }
}

function sameMark(left: MarkState | undefined, right: MarkState): boolean {
  return left !== undefined && left.mark === right.mark && left.markedAt === right.markedAt
}

export function useAdMark() {
  const queryClient = useQueryClient()
  const text = useText()
  const clock = useClock()
  const [errors, setErrors] = useState<Readonly<Record<string, string>>>({})

  function setError(adId: string, error: string | undefined) {
    setErrors((current) => {
      const rest = Object.fromEntries(Object.entries(current).filter(([key]) => key !== adId))
      return error === undefined ? rest : { ...rest, [adId]: error }
    })
  }

  function writeMark(adId: string, state: MarkState) {
    queryClient.setQueryData<AdsSnapshot>(QUERY_KEYS.ads, (snapshot) => snapshot && markAd(snapshot, adId, state.mark, state.markedAt))
  }

  const command = useCommand<MarkDraft, MarkContext | undefined>({
    onMutate: (draft) => {
      const previous = markOf(queryClient.getQueryData<AdsSnapshot>(QUERY_KEYS.ads), draft.ad_id)
      setError(draft.ad_id, undefined)
      if (previous === undefined) return undefined
      const written: MarkState = draft.type === 'ad.mark' ? { mark: draft.mark, markedAt: clock().toISOString() } : { mark: null, markedAt: null }
      writeMark(draft.ad_id, written)
      return { previous, written }
    },
    onError: (error, draft, context) => {
      setError(draft.ad_id, commandErrorText(error, text))
      if (context === undefined) return
      if (sameMark(markOf(queryClient.getQueryData<AdsSnapshot>(QUERY_KEYS.ads), draft.ad_id), context.written)) writeMark(draft.ad_id, context.previous)
    },
  })

  return {
    errors,
    mark: (adId: string, mark: Mark) => command.mutate({ type: 'ad.mark', v: 1, ad_id: adId, mark }),
    unmark: (adId: string) => command.mutate({ type: 'ad.unmark', v: 1, ad_id: adId }),
  }
}
