import { screen } from '@testing-library/react'
import { describe, it } from 'vitest'
import { routes } from '@/app/router'
import { demoSnapshots } from '@/features/demo/demoSnapshots'
import { nb } from '@/shared/i18n/nb'
import { stubDialogs } from '@/test/dialog'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegianRoutes } from '@/test/render'
import { warmRoutes } from '@/test/warmRoutes'

const SAMPLE = demoSnapshots('nb', nb)
const MACHINE_TEXTS = [
  ...SAMPLE.ads.active.flatMap((ad) => [ad.company, ad.title, ad.place]),
  ...SAMPLE.ads.hidden.flatMap((ad) => [ad.company, ad.title]),
  ...SAMPLE.ads.closed.flatMap((ad) => [ad.company, ad.title]),
  ...SAMPLE.applications.applications.flatMap((application) => [application.company, application.title]),
  ...SAMPLE.models.providers.flatMap((provider) => provider.models.map((model) => model.name)),
]

describe('app views in bokmål', () => {
  stubDialogs()
  warmRoutes(routes)

  it.each([
    '/demo/today',
    '/demo/active',
    '/demo/hidden',
    '/demo/closed',
    '/demo/all',
    '/demo/ad?id=nav-havbris',
    '/demo/applications',
    '/demo/applications/2',
    '/demo/settings/search',
    '/demo/settings/delivery',
    '/demo/settings/appearance',
    '/demo/settings/model',
    '/demo/settings/matching',
    '/demo/settings/connection',
    '/demo/settings/profile',
  ])('should show %s without English text', async (path) => {
    renderNorwegianRoutes(routes, path)

    await screen.findByRole('heading', { level: 1 })

    expectNoEnglish(MACHINE_TEXTS)
  })
})
