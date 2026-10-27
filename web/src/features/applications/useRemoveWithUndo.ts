import { useCallback, useEffect, useRef, useState } from 'react'
import type { Application } from '@/features/relay/contract/payload.gen'

export const UNDO_WINDOW_MS = 8000

export interface RemoveWithUndo {
  pending: Application | undefined
  remove: (application: Application) => void
  undo: () => void
}

export function useRemoveWithUndo(onRemove: (applicationId: number) => void): RemoveWithUndo {
  const [pending, setPending] = useState<Application | undefined>(undefined)
  const pendingRef = useRef<Application | undefined>(undefined)
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const onRemoveRef = useRef(onRemove)

  useEffect(() => {
    onRemoveRef.current = onRemove
  }, [onRemove])

  const clear = useCallback(() => {
    clearTimeout(timerRef.current)
    timerRef.current = undefined
    pendingRef.current = undefined
    setPending(undefined)
  }, [])

  const flush = useCallback(() => {
    const application = pendingRef.current
    if (!application) return
    clear()
    onRemoveRef.current(application.id)
  }, [clear])

  const remove = useCallback(
    (application: Application) => {
      flush()
      pendingRef.current = application
      setPending(application)
      timerRef.current = setTimeout(flush, UNDO_WINDOW_MS)
    },
    [flush],
  )

  useEffect(() => {
    function onVisibilityChange() {
      if (document.visibilityState === 'hidden') flush()
    }
    document.addEventListener('visibilitychange', onVisibilityChange)
    return () => {
      document.removeEventListener('visibilitychange', onVisibilityChange)
      flush()
    }
  }, [flush])

  return { pending, remove, undo: clear }
}
