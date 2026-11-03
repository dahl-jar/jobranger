import { useEffect } from 'react'

const SITE_NAME = 'jobranger'

export function usePageTitle(page?: string): void {
  useEffect(() => {
    document.title = page === undefined ? SITE_NAME : `${page} · ${SITE_NAME}`
  }, [page])
}
