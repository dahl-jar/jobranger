import { QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { QUERY_KEYS } from '@/app/queryKeys'
import { useOptimisticWrite } from '@/app/useOptimisticWrite'
import { createConnectionStore } from '@/features/relay/connectionStore'
import type { ConfigSnapshot, ConfigUpdate, Saved } from '@/features/relay/contract/payload.gen'
import { type Relay, RelayContext } from '@/features/relay/relayContext'
import { heldSend, refusal, SAVED, type SendMock, SNAPSHOTS } from '@/test/appData'

type Draft = Omit<ConfigUpdate, 'id'>

const DRAFT: Draft = { type: 'config.update', v: 1, patch: { language: 'nb' } }
const NEWER: ConfigSnapshot = { ...SNAPSHOTS.config, roles: [] }

interface Callbacks {
  holds?: (current: ConfigSnapshot, draft: Draft, written: ConfigSnapshot) => boolean
  onError?: (error: Error, draft: Draft) => void
  onSuccess?: (saved: Saved, draft: Draft) => void
}

function renderWrite(send: SendMock, seeded: ConfigSnapshot | undefined, callbacks: Callbacks = {}) {
  const queryClient = createQueryClient()
  if (seeded !== undefined) queryClient.setQueryData(QUERY_KEYS.config, seeded)
  const store = createConnectionStore()
  store.dispatch({ kind: 'ready' })
  const relay: Relay = { store, send, unpair: vi.fn(async () => undefined), retryNow: vi.fn() }
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <RelayContext value={relay}>{children}</RelayContext>
    </QueryClientProvider>
  )
  const view = renderHook(
    () =>
      useOptimisticWrite<ConfigSnapshot, Draft>({
        queryKey: QUERY_KEYS.config,
        apply: (data, draft) => ({ ...data, language: draft.patch.language ?? data.language }),
        holds: (current, _draft, written) => current === written,
        restore: (_current, _draft, previous) => previous,
        ...callbacks,
      }),
    { wrapper },
  )
  return { ...view, queryClient }
}

describe('useOptimisticWrite', () => {
  it('should write the change to the cache while the command is pending', () => {
    const held = heldSend()
    const { result, queryClient } = renderWrite(held.send, SNAPSHOTS.config)

    act(() => result.current.mutate(DRAFT))

    expect(queryClient.getQueryData<ConfigSnapshot>(QUERY_KEYS.config)?.language).toBe('nb')
  })

  it('should restore the previous data when the command fails', async () => {
    const held = heldSend()
    const { result, queryClient } = renderWrite(held.send, SNAPSHOTS.config)
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())

    act(() => held.reject())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toEqual(SNAPSHOTS.config)
  })

  it('should keep newer cache data when the command fails', async () => {
    const held = heldSend()
    const { result, queryClient } = renderWrite(held.send, SNAPSHOTS.config)
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())
    queryClient.setQueryData(QUERY_KEYS.config, NEWER)

    act(() => held.reject())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toEqual(NEWER)
  })

  it('should leave a missing cache entry empty when the command fails', async () => {
    const held = heldSend()
    const { result, queryClient } = renderWrite(held.send, undefined)
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())

    act(() => held.reject())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toBeUndefined()
  })

  it('should report the error when the cache filled after the command was sent', async () => {
    const held = heldSend()
    const onError = vi.fn()
    const error = refusal()
    const { result, queryClient } = renderWrite(held.send, undefined, { onError })
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())
    queryClient.setQueryData(QUERY_KEYS.config, SNAPSHOTS.config)

    act(() => held.reject(error))

    await waitFor(() => expect(onError).toHaveBeenCalledWith(error, DRAFT))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toEqual(SNAPSHOTS.config)
  })

  it('should leave a cleared cache empty when the command fails', async () => {
    const held = heldSend()
    const onError = vi.fn()
    const holds = vi.fn(() => true)
    const { result, queryClient } = renderWrite(held.send, SNAPSHOTS.config, { onError, holds })
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())
    queryClient.clear()

    act(() => held.reject())

    await waitFor(() => expect(onError).toHaveBeenCalled())
    expect([queryClient.getQueryData(QUERY_KEYS.config), holds.mock.calls]).toEqual([undefined, []])
  })

  it('should report the error with its draft when the command fails', async () => {
    const held = heldSend()
    const onError = vi.fn()
    const error = refusal()
    const { result } = renderWrite(held.send, SNAPSHOTS.config, { onError })
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())

    act(() => held.reject(error))

    await waitFor(() => expect(onError).toHaveBeenCalledWith(error, DRAFT))
  })

  it('should report the save with its draft when the command succeeds', async () => {
    const held = heldSend()
    const onSuccess = vi.fn()
    const { result } = renderWrite(held.send, SNAPSHOTS.config, { onSuccess })
    act(() => result.current.mutate(DRAFT))
    await waitFor(() => expect(held.send).toHaveBeenCalled())

    act(() => held.resolve())

    await waitFor(() => expect(onSuccess).toHaveBeenCalledWith(SAVED, DRAFT))
  })
})
