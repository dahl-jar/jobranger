import { afterEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { QUERY_KEYS } from '@/app/queryKeys'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'

const TEN_MINUTES_MS = 600_000

afterEach(() => {
  vi.useRealTimers()
})

describe('createQueryClient', () => {
  it('should keep a pushed snapshot that nothing reads for ten minutes', async () => {
    vi.useFakeTimers()
    const client = createQueryClient()
    client.setQueryData(QUERY_KEYS.ads, payloadFixtures['ads.snapshot'])

    await vi.advanceTimersByTimeAsync(TEN_MINUTES_MS)

    expect(client.getQueryData(QUERY_KEYS.ads)).toEqual(payloadFixtures['ads.snapshot'])
  })
})
