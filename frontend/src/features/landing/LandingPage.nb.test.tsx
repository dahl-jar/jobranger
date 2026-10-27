import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/router'
import { en } from '@/shared/i18n/en'
import { nb } from '@/shared/i18n/nb'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegianRoutes } from '@/test/render'

const PHONE_WIDTH_PX = 390
const PAGE_GUTTER_PX = 16
const NAV_GAP_PX = 22
const NAV_CHAR_PX = 8.4

function navWidth(text: typeof en): number {
  const { how, install, free, faq } = text.landing.nav
  const labels = [how, install, free, faq]
  return labels.join('').length * NAV_CHAR_PX + (labels.length - 1) * NAV_GAP_PX
}

describe('LandingPage in bokmål', () => {
  it('should fit the bokmål nav on one line at phone width', () => {
    expect(navWidth(nb)).toBeLessThanOrEqual(PHONE_WIDTH_PX - 2 * PAGE_GUTTER_PX)
  })

  it('should show the landing page without English text', async () => {
    renderNorwegianRoutes(routes, '/')

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'jobranger sjekker norske annonser innen programvare og data engineering mot CV-en din.',
      }),
    ).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should number the how-it-works steps from 01', async () => {
    renderNorwegianRoutes(routes, '/')

    const section = (await screen.findByRole('heading', { level: 2, name: 'Slik virker det' })).closest('section')

    expect(within(section ?? document.body).getAllByText(/^0\d$/).map((number) => number.textContent)).toEqual(['01', '02', '03', '04'])
  })

  it('should show the example due dates with Norwegian months', async () => {
    renderNorwegianRoutes(routes, '/')

    expect(await screen.findByText('10. okt')).toBeInTheDocument()
    expect(screen.getByText('18. okt')).toBeInTheDocument()
  })
})
