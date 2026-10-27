import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { foundApplication, handApplication } from '@/features/applications/testData'
import { UNDO_WINDOW_MS, useRemoveWithUndo } from '@/features/applications/useRemoveWithUndo'

function setVisibility(state: DocumentVisibilityState) {
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: state })
  document.dispatchEvent(new Event('visibilitychange'))
}

function renderRemoval() {
  const onRemove = vi.fn()
  const view = renderHook(() => useRemoveWithUndo(onRemove))
  act(() => {
    view.result.current.remove(foundApplication)
  })
  return { ...view, onRemove }
}

describe('useRemoveWithUndo', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    setVisibility('visible')
  })

  it('should send the removal only after eight seconds', () => {
    const { onRemove } = renderRemoval()

    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS - 1)
    })
    expect(onRemove).not.toHaveBeenCalled()
    act(() => {
      vi.advanceTimersByTime(1)
    })

    expect(onRemove).toHaveBeenCalledExactlyOnceWith(foundApplication.id)
  })

  it('should hold the application as pending until the window ends', () => {
    const { result } = renderRemoval()
    expect(result.current.pending).toBe(foundApplication)

    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS)
    })

    expect(result.current.pending).toBeUndefined()
  })

  it('should not send the removal after Undo', () => {
    const { result, onRemove } = renderRemoval()

    act(() => {
      result.current.undo()
    })
    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS)
    })

    expect(onRemove).not.toHaveBeenCalled()
    expect(result.current.pending).toBeUndefined()
  })

  it('should send the removal at once when the page is hidden', () => {
    const { onRemove } = renderRemoval()

    act(() => {
      setVisibility('hidden')
    })

    expect(onRemove).toHaveBeenCalledExactlyOnceWith(foundApplication.id)
  })

  it('should keep the removal pending when the page becomes visible', () => {
    const { result, onRemove } = renderRemoval()

    act(() => {
      setVisibility('visible')
    })

    expect(onRemove).not.toHaveBeenCalled()
    expect(result.current.pending).toBe(foundApplication)
  })

  it('should send the removal at once when the page is left', () => {
    const { unmount, onRemove } = renderRemoval()

    unmount()

    expect(onRemove).toHaveBeenCalledExactlyOnceWith(foundApplication.id)
  })

  it('should send the earlier removal when a second one starts', () => {
    const { result, onRemove } = renderRemoval()

    act(() => {
      result.current.remove(handApplication)
    })
    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS)
    })

    expect(onRemove.mock.calls).toEqual([[foundApplication.id], [handApplication.id]])
  })

  it('should give the next removal its own eight seconds after Undo', () => {
    const { result, onRemove } = renderRemoval()
    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS / 2)
      result.current.undo()
      result.current.remove(handApplication)
    })

    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS / 2)
    })

    expect(onRemove).not.toHaveBeenCalled()
  })

  it('should send through the latest callback', () => {
    const first = vi.fn()
    const latest = vi.fn()
    const { result, rerender } = renderHook(({ onRemove }) => useRemoveWithUndo(onRemove), { initialProps: { onRemove: first } })
    act(() => {
      result.current.remove(foundApplication)
    })
    rerender({ onRemove: latest })

    act(() => {
      vi.advanceTimersByTime(UNDO_WINDOW_MS)
    })

    expect(first).not.toHaveBeenCalled()
    expect(latest).toHaveBeenCalledExactlyOnceWith(foundApplication.id)
  })
})
