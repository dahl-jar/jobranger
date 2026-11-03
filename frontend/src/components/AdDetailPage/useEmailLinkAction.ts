import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { useAppBase } from '@/hooks/useAppBase'
import { deepLinkOutcome, parseDeepLink } from '@/helpers/adDeepLink'
import { type DeepLink, detailPath } from '@/helpers/adPaths'
import type { AdsSnapshot, Mark } from '@/contract/payload.gen'

interface EmailLinkInput {
  ads: AdsSnapshot | undefined
  mark: (adId: string, mark: Mark) => void
}

interface EmailLinkAction {
  choice: DeepLink | undefined
  confirmChoice: () => void
  notice: DeepLink | undefined
  clearNotice: () => void
}

export function useEmailLinkAction({ ads, mark }: EmailLinkInput): EmailLinkAction {
  const location = useLocation()
  const navigate = useNavigate()
  const base = useAppBase()
  const handledKey = useRef<string | undefined>(undefined)
  const [choice, setChoice] = useState<DeepLink | undefined>(undefined)
  const [notice, setNotice] = useState<DeepLink | undefined>(undefined)

  const openLink = useEffectEvent(() => {
    const link = parseDeepLink(location.pathname, location.search)
    if (link === undefined || ads === undefined || handledKey.current === location.key) return
    handledKey.current = location.key
    const outcome = deepLinkOutcome(ads, link)
    setChoice(outcome === 'mark' ? link : undefined)
    setNotice(outcome === 'marked' ? link : undefined)
    void navigate(detailPath(link.adId, base), { replace: true, state: location.state })
  })

  useEffect(() => openLink(), [ads, location.key])

  function confirmChoice() {
    if (choice === undefined) return
    mark(choice.adId, choice.action)
    setChoice(undefined)
    setNotice(choice)
  }

  return { choice, confirmChoice, notice, clearNotice: () => setNotice(undefined) }
}
