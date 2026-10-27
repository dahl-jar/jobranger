import { useEffect } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { parseDeepLink, savePendingDeepLink, takePendingDeepLink } from '@/features/jobs/deepLink'
import { useConnection } from '@/features/relay/relayContext'

export function usePendingLinkHandoff(): void {
  const isUnpaired = useConnection().kind === 'unpaired'
  const { pathname, search } = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    const pending = takePendingDeepLink()
    if (pending !== undefined) void navigate(pending, { replace: true })
  }, [navigate])

  useEffect(() => {
    if (isUnpaired && parseDeepLink(pathname, search) !== undefined) savePendingDeepLink(`${pathname}${search}`)
  }, [isUnpaired, pathname, search])
}
