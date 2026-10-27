import { en, type Messages } from '@/shared/i18n/en'
import { nb } from '@/shared/i18n/nb'
import { expect, test } from './consoleErrors'

interface Screen {
  name: string
  path: string
  heading: (text: Messages) => string
}

const LANGUAGES = [
  { language: 'en', locale: 'en-GB', text: en },
  { language: 'nb', locale: 'nb-NO', text: nb },
] as const

const VIEWPORTS = [
  { name: 'desktop', use: { viewport: { width: 1440, height: 900 } } },
  { name: 'mobile', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
] as const

const SCREENS: readonly Screen[] = [
  { name: 'landing', path: '/', heading: (text) => text.landing.headline },
  { name: 'setup', path: '/setup', heading: (text) => text.setup.steps.install.title },
  { name: 'pairing', path: '/pair', heading: (text) => text.pairing.titles.connect },
  { name: 'demo jobs', path: '/demo/today', heading: (text) => text.jobs.lists.today },
  { name: 'demo applications', path: '/demo/applications', heading: (text) => text.applications.heading },
  { name: 'demo settings', path: '/demo/settings/search', heading: (text) => text.settings.nav },
]

for (const viewport of VIEWPORTS) {
  for (const { language, locale, text } of LANGUAGES) {
    test.describe(`${viewport.name} in ${language}`, () => {
      test.use({ ...viewport.use, locale })

      for (const screen of SCREENS) {
        test(`should show the ${screen.name} screen without console errors`, async ({ page, consoleErrors }) => {
          await page.goto(screen.path)

          await expect(page.getByRole('heading', { level: 1, name: screen.heading(text) })).toBeVisible()
          await expect(page.locator('html')).toHaveAttribute('lang', language)
          expect(consoleErrors).toEqual([])
        })
      }
    })
  }
}
