import { useState } from 'react'

export function useDraft<T>(saved: T, isSame: (first: T, second: T) => boolean = Object.is) {
  const [draft, setDraft] = useState(saved)
  const [base, setBase] = useState(saved)
  if (!isSame(base, saved)) {
    setBase(saved)
    setDraft(saved)
  }
  return [draft, setDraft] as const
}
