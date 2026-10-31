import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { LandingPage } from '@/features/landing/LandingPage'
import { CLAIMS, HOW_STEPS, PRIVACY_LINES } from '@/features/landing/landingContent'
import { SOURCE_URL } from '@/shared/config'
import { renderRoutes } from '@/test/render'

function renderLanding() {
  return renderRoutes([{ path: '/', Component: LandingPage }], '/')
}

async function exampleAd(title: string) {
  const article = (await screen.findByRole('heading', { name: title })).closest('article')
  if (article === null) throw new Error(`no example ad article holds ${title}`)
  return within(article)
}

describe('LandingPage', () => {
  it('should link each install step to its walkthrough step', async () => {
    renderLanding()

    const links = await screen.findAllByRole('link', { name: 'See this step' })

    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/setup?step=2', '/setup?step=7', '/setup?step=8'])
  })

  it.each([
    { name: 'Install, setup walkthrough', href: '/setup' },
    { name: 'Connect your machine', href: '/pair' },
    { name: 'Source code', href: SOURCE_URL },
    { name: 'Setup walkthrough', href: '/setup' },
    { name: 'Open the app', href: '/app' },
  ])('should link $name to $href', async ({ name, href }) => {
    renderLanding()

    expect(await screen.findByRole('link', { name })).toHaveAttribute('href', href)
  })

  it('should show the hidden line under the example matches', async () => {
    renderLanding()

    expect(await screen.findByText("41 hidden: 18 senior · 12 need 3+ yrs · 6 master's · 5 other roles")).toBeInTheDocument()
  })

  it.each([
    { title: 'Data Engineer', meta: 'Havbris Data AS · Trondheim · NAV' },
    { title: 'Platform Engineer', meta: 'Kystlinje AS · Oslo · Teamtailor' },
  ])('should show the source line of $title', async ({ title, meta }) => {
    renderLanding()

    const ad = await exampleAd(title)

    expect(ad.getByText(meta)).toBeInTheDocument()
  })

  it.each([
    { title: 'Data Engineer', fit: 'Strong fit' },
    { title: 'Platform Engineer', fit: 'Partial fit' },
  ])('should mark $title as $fit', async ({ title, fit }) => {
    renderLanding()

    const ad = await exampleAd(title)

    expect(ad.getByText(fit)).toBeInTheDocument()
  })

  it('should show the checks of the strong fit example', async () => {
    renderLanding()

    const ad = await exampleAd('Data Engineer')

    expect(ad.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Requires 1–3 years of experience met',
      'Bachelor in IT met',
      'Azure preferred, not in your CV partly',
    ])
  })

  it.each([
    { section: 'claims', lines: CLAIMS },
    { section: 'how it works steps', lines: HOW_STEPS.map((step) => step.title) },
    { section: 'data storage lines', lines: PRIVACY_LINES },
  ])('should show every line of the $section', async ({ lines }) => {
    renderLanding()
    await screen.findByRole('heading', { level: 1 })

    for (const line of lines) expect(screen.getByText(line)).toBeInTheDocument()
  })

  it('should show the due note and summary of the strong fit example', async () => {
    renderLanding()

    const ad = await exampleAd('Data Engineer')

    expect(ad.getByText('in 2 days')).toBeInTheDocument()
    expect(ad.getByText('Build pipelines and data models for a new data platform.')).toBeInTheDocument()
  })

  it('should leave out the summary when an example has none', async () => {
    renderLanding()

    const ad = await exampleAd('Platform Engineer')

    expect(ad.queryByRole('paragraph')).not.toBeInTheDocument()
  })

  it('should leave out the check list when an example has no checks', async () => {
    renderLanding()

    const ad = await exampleAd('Platform Engineer')

    expect(ad.queryByRole('list')).not.toBeInTheDocument()
  })

  it.each(['How it works', 'Install and set up', 'Cost', 'Where your data is stored', 'Questions'])('should head a section with %s', async (name) => {
    renderLanding()

    expect(await screen.findByRole('heading', { level: 2, name })).toBeInTheDocument()
  })

  it('should head the example panel one level below the page title', async () => {
    renderLanding()

    expect(await screen.findByRole('heading', { level: 2, name: 'Today, 3 matches' })).toBeInTheDocument()
  })
})
