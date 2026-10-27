import { en } from '@/shared/i18n/en'
import { expect, test } from '@playwright/test'

const DEMO_PATH = '/demo'
const PAIR_PATH = '/pair'

test.use({ locale: 'en-GB' })

test('should leave the demo for pairing after Settings > Connection > Unpair', async ({ page }) => {
  await page.goto(DEMO_PATH)
  await page.getByRole('link', { name: en.settings.nav }).click()
  await page.getByRole('link', { name: en.settings.sections.connection.label }).click()
  await page.getByRole('button', { name: en.settings.connection.unpair }).click()

  await page.getByRole('button', { name: en.settings.connection.confirmYes }).click()

  await expect(page).toHaveURL(PAIR_PATH)
  await expect(page.getByRole('heading', { level: 1, name: en.pairing.titles.connect })).toBeVisible()
})
