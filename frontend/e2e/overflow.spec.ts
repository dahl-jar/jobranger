import { en } from '@/shared/i18n/en'
import { expect, test } from '@playwright/test'

const SETUP_PATH = '/setup'
const NARROW_VIEWPORT = { width: 320, height: 700 }
const LONG_SUBTITLE = 'x'.repeat(200)

test.use({ viewport: NARROW_VIEWPORT, locale: 'en-GB' })

test('should not scroll sideways at 320 px with a long unbroken subtitle', async ({ page }) => {
  await page.goto(SETUP_PATH)
  await page.getByText(en.setup.subtitle, { exact: true }).evaluate((subtitle, text) => {
    subtitle.textContent = text
  }, LONG_SUBTITLE)

  const widths = await page.evaluate(() => ({ scroll: document.documentElement.scrollWidth, client: document.documentElement.clientWidth }))

  expect(widths.scroll).toBeLessThanOrEqual(widths.client)
})
