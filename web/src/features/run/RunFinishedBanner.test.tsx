import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import styles from '@/features/run/run.module.css'
import { RunFinishedBanner } from '@/features/run/RunFinishedBanner'
import type { EndedRun } from '@/features/run/runView'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { RunFinished } from '@/features/relay/contract/payload.gen'
import button from '@/shared/ui/button.module.css'
import { renderRoutes } from '@/test/render'

const finished: RunFinished = {
  ...payloadFixtures['run.finished'],
  finished_at: new Date(2026, 9, 12, 13, 6).toISOString(),
  failed_sources: [],
}

function renderBanner(view: EndedRun, onDismiss = vi.fn()) {
  return renderRoutes(
    [
      { path: '/', element: <RunFinishedBanner view={view} onDismiss={onDismiss} /> },
      { path: '/app/today', element: null },
    ],
    '/',
  )
}

function renderFinished(result: Partial<RunFinished>) {
  return renderBanner({ kind: 'finished', result: { ...finished, ...result } })
}

describe('RunFinishedBanner', () => {
  it('should show when the run finished', () => {
    renderFinished({})

    expect(screen.getByText('Run finished at 13:06')).toBeInTheDocument()
  })

  it('should show the counts of a finished run', () => {
    renderFinished({})

    expect(screen.getByText('37 new ads · 6 matches · 31 hidden · $0.41')).toBeInTheDocument()
  })

  it('should use singular words for one ad and one match', () => {
    renderFinished({ new_ads: 1, matches: 1, hidden: 0 })

    expect(screen.getByText('1 new ad · 1 match · 0 hidden · $0.41')).toBeInTheDocument()
  })

  it('should name the sources that could not be read', () => {
    renderFinished({ failed_sources: ['linkedin', 'finn'] })

    expect(screen.getByText('Could not read LinkedIn, FINN.')).toBeInTheDocument()
  })

  it('should not mention unread sources when every source was read', () => {
    renderFinished({})

    expect(screen.queryByText(/Could not read/)).not.toBeInTheDocument()
  })

  it('should say AI checks paused when the monthly limit was reached', () => {
    renderFinished({ budget_paused: true })

    expect(screen.getByText('AI checks paused at your monthly limit.')).toBeInTheDocument()
  })

  it('should not mention the monthly limit when AI checks ran', () => {
    renderFinished({ budget_paused: false })

    expect(screen.queryByText('AI checks paused at your monthly limit.')).not.toBeInTheDocument()
  })

  it('should link See new matches to the Today list', () => {
    renderFinished({})

    expect(screen.getByRole('link', { name: 'See new matches' })).toHaveAttribute('href', '/app/today')
  })

  it('should show See new matches as a primary button', () => {
    renderFinished({})

    expect(screen.getByRole('link', { name: 'See new matches' }).className).toBe(
      `${button.primary} ${button.size36} ${styles.seeNew}`,
    )
  })

  it('should dismiss the banner when See new matches is followed', async () => {
    const onDismiss = vi.fn()
    renderBanner({ kind: 'finished', result: finished }, onDismiss)

    await userEvent.click(screen.getByRole('link', { name: 'See new matches' }))

    expect(onDismiss).toHaveBeenCalledOnce()
  })

  it('should say the run was cancelled', () => {
    renderBanner({ kind: 'cancelled', runId: 42 })

    expect(screen.getByText('Run cancelled.')).toBeInTheDocument()
  })

  it('should dismiss the banner when Dismiss is pressed', async () => {
    const onDismiss = vi.fn()
    renderBanner({ kind: 'cancelled', runId: 42 }, onDismiss)

    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }))

    expect(onDismiss).toHaveBeenCalledOnce()
  })
})
