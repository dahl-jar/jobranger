import { QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/components/App/createQueryClient'
import { QUERY_KEYS } from '@/constants/queryKeys'
import { useInstantSave } from '@/hooks/useInstantSave'
import { createConnectionStore } from '@/services/relayConnection/connectionStore'
import type { ConfigSnapshot, ConfigUpdate, Saved } from '@/contract/payload.gen'
import { type Relay, RelayContext } from '@/hooks/useRelay'
import { heldSend, refusal, SAVED, type SendMock, SNAPSHOTS } from '@/__tests__/appPayloadMocks'

type Draft = Omit<ConfigUpdate, 'id'>

const DRAFT: Draft = { type: 'config.update', v: 1, patch: { language: 'nb' } }
const NEWER: ConfigSnapshot = { ...SNAPSHOTS.config, roles: [] }

interface Callbacks {
  holds?: (current: ConfigSnapshot, draft: Draft, written: ConfigSnapshot) => boolean
  onError?: (error: Error, draft: Draft) => void
  onSuccess?: (saved: Saved, draft: Draft) => void
}

function renderInstantSave(send: SendMock, seeded: ConfigSnapshot | undefined, callbacks: Callbacks = {}) {
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
      useInstantSave<ConfigSnapshot, Draft>({
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

async function startHeldSave(seeded: ConfigSnapshot | undefined, callbacks: Callbacks = {}) {
  const held = heldSend()
  const { result, queryClient } = renderInstantSave(held.send, seeded, callbacks)
  act(() => result.current.mutate(DRAFT))
  await waitFor(() => expect(held.send).toHaveBeenCalled())
  return { held, result, queryClient }
}

describe('useInstantSave', () => {
  it('should write the change to the cache while the command is pending', () => {
    const held = heldSend()
    const { result, queryClient } = renderInstantSave(held.send, SNAPSHOTS.config)

    act(() => result.current.mutate(DRAFT))

    expect(queryClient.getQueryData<ConfigSnapshot>(QUERY_KEYS.config)?.language).toBe('nb')
  })

  it('should restore the previous data when the command fails', async () => {
    const { held, result, queryClient } = await startHeldSave(SNAPSHOTS.config)

    act(() => held.reject())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toEqual(SNAPSHOTS.config)
  })

  it('should keep newer cache data when the command fails', async () => {
    const { held, result, queryClient } = await startHeldSave(SNAPSHOTS.config)
    queryClient.setQueryData(QUERY_KEYS.config, NEWER)

    act(() => held.reject())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toEqual(NEWER)
  })

  it('should leave a missing cache entry empty when the command fails', async () => {
    const { held, result, queryClient } = await startHeldSave(undefined)

    act(() => held.reject())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toBeUndefined()
  })

  it('should report the error when the cache filled after the command was sent', async () => {
    const onError = vi.fn()
    const error = refusal()
    const { held, queryClient } = await startHeldSave(undefined, { onError })
    queryClient.setQueryData(QUERY_KEYS.config, SNAPSHOTS.config)

    act(() => held.reject(error))

    await waitFor(() => expect(onError).toHaveBeenCalledWith(error, DRAFT))
    expect(queryClient.getQueryData(QUERY_KEYS.config)).toEqual(SNAPSHOTS.config)
  })

  it('should leave a cleared cache empty when the command fails', async () => {
    const onError = vi.fn()
    const holds = vi.fn(() => true)
    const { held, queryClient } = await startHeldSave(SNAPSHOTS.config, { onError, holds })
    queryClient.clear()

    act(() => held.reject())

    await waitFor(() => expect(onError).toHaveBeenCalled())
    expect([queryClient.getQueryData(QUERY_KEYS.config), holds.mock.calls]).toEqual([undefined, []])
  })

  it('should report the error with its draft when the command fails', async () => {
    const onError = vi.fn()
    const error = refusal()
    const { held } = await startHeldSave(SNAPSHOTS.config, { onError })

    act(() => held.reject(error))

    await waitFor(() => expect(onError).toHaveBeenCalledWith(error, DRAFT))
  })

  it('should report the save with its draft when the command succeeds', async () => {
    const onSuccess = vi.fn()
    const { held } = await startHeldSave(SNAPSHOTS.config, { onSuccess })

    act(() => held.resolve())

    await waitFor(() => expect(onSuccess).toHaveBeenCalledWith(SAVED, DRAFT))
  })
})
