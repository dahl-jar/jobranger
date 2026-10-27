import { describe, expect, it } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { ConfigPatch } from '@/features/relay/contract/payload.gen'
import { applyConfigPatch } from '@/features/settings/configPatch'

const CONFIG = payloadFixtures['config.snapshot']

describe('applyConfigPatch', () => {
  it.each<{ section: string; patch: ConfigPatch; expected: object }>([
    { section: 'targets', patch: { targets: { places: [] } }, expected: { targets: { ...CONFIG.targets, places: [] } } },
    { section: 'sources', patch: { sources: { finn: true } }, expected: { sources: { ...CONFIG.sources, finn: true } } },
    { section: 'schedule', patch: { schedule: { times: ['06:00'] } }, expected: { schedule: { ...CONFIG.schedule, times: ['06:00'] } } },
    { section: 'notify', patch: { notify: { website: false } }, expected: { notify: { ...CONFIG.notify, website: false } } },
    {
      section: 'model',
      patch: { model: { monthly_limit_usd: null } },
      expected: { model: { ...CONFIG.model, monthly_limit_usd: null } },
    },
  ])('should change only the patched field in $section', ({ patch, expected }) => {
    expect(applyConfigPatch(CONFIG, patch)).toEqual({ ...CONFIG, ...expected })
  })

  it('should keep the email address when only email on changes', () => {
    const next = applyConfigPatch(CONFIG, { notify: { email: { on: false } } })

    expect(next.notify.email).toEqual({ on: false, address: CONFIG.notify.email.address })
  })

  it('should keep the output price when only the input price changes', () => {
    const next = applyConfigPatch(CONFIG, { model: { price: { input_per_mtok: 1 } } })

    expect(next.model.price).toEqual({ input_per_mtok: 1, output_per_mtok: CONFIG.model.price.output_per_mtok })
  })
})
