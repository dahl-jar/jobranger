import { describe, expect, it } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Recheck } from '@/features/relay/contract/payload.gen'
import { recheckText } from '@/features/settings/recheckText'

describe('recheckText', () => {
  it.each<[string, Recheck | undefined, string | undefined]>([
    ['add the cost to a priced recheck', payloadFixtures.saved.recheck, 'Rechecks 37 ads on the next run, up to about $0.12'],
    ['leave the cost off an unpriced recheck', payloadFixtures['saved.unpriced'].recheck, 'Rechecks 37 ads on the next run'],
    ['say ad for one ad', { ads: 1, cost_usd: null }, 'Rechecks 1 ad on the next run'],
    ['say nothing for zero ads', { ads: 0, cost_usd: 0 }, undefined],
    ['say nothing without a recheck', undefined, undefined],
  ])('should %s', (_, recheck, text) => {
    expect(recheckText(recheck)).toBe(text)
  })
})
