import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SetupPage } from '@/features/setup/SetupPage'
import { setupSteps } from '@/features/setup/setupSteps'
import { nb } from '@/shared/i18n/nb'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegianRoutes } from '@/test/render'

const SITE_PAIR_URL = 'https://site-host.example/pair'
const TERMINAL_COLUMNS = 72
const STEP_COUNT = setupSteps(SITE_PAIR_URL).length

function renderStep(step: number) {
  return renderNorwegianRoutes([{ path: '/setup', Component: SetupPage }], `/setup?step=${step}`)
}

describe('SetupPage in bokmål', () => {
  it.each(Array.from({ length: STEP_COUNT }, (_, index) => index + 1))('should show step %i without English text', async (step) => {
    renderStep(step)

    expect(await screen.findByText(`Steg ${step} av ${STEP_COUNT}`)).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should name the page title in bokmål', async () => {
    renderStep(2)

    await screen.findByRole('heading', { level: 1, name: 'Modell og nøkkel' })

    expect(document.title).toBe(`Oppsett, steg 2 av ${STEP_COUNT} · jobranger`)
  })

  it('should keep every terminal line within the terminal width', () => {
    const lines = setupSteps(SITE_PAIR_URL, nb).flatMap((step) => step.lines.map(([, line]) => line))

    expect(lines.filter((line) => line.length > TERMINAL_COLUMNS)).toEqual([])
  })

  it('should keep a space between the fit and the title in the dry run', () => {
    const dryRun = setupSteps(SITE_PAIR_URL, nb).find((step) => step.id === 'dryRun')

    expect(dryRun?.lines.map(([, line]) => line)).toContain('  Passer delvis  Platform Engineer   Kystlinje AS     Oslo       18.\u00a0okt')
  })
})
