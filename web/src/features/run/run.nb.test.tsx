import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { RunProgress as RunProgressMessage, SourceStep, Status } from '@/features/relay/contract/payload.gen'
import { RunFinishedBanner } from '@/features/run/RunFinishedBanner'
import { RunLine } from '@/features/run/RunLine'
import { RunNowButton } from '@/features/run/RunNowButton'
import { RunProgress } from '@/features/run/RunProgress'
import { formatClock } from '@/shared/format/dates'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian } from '@/test/render'

const progress = payloadFixtures['run.progress']
const status = payloadFixtures['status']
const NEXT_RUN_AT = '2026-10-12T13:00:00Z'
const LAST_RUN_AT = status.last_run?.finished_at ?? ''
const idle: Status = { ...status, running: null, next_run_at: NEXT_RUN_AT }
const pendingModel: RunProgressMessage['model'] = { state: 'pending', checked: 0, total: 0, results_by: null }

function withSources(states: SourceStep['state'][], model: RunProgressMessage['model'] = pendingModel): RunProgressMessage {
  const sources = (['nav', 'teamtailor', 'finn', 'ashby'] as const).map((source, index) => ({
    source,
    state: states[index] ?? 'pending',
    found_new: states[index] === 'done' ? 3 : null,
  }))
  return { ...progress, sources, model }
}

describe('run in bokmål', () => {
  it.each([
    { name: 'every step state', run: withSources(['done', 'active', 'failed', 'pending'], { ...pendingModel, state: 'skipped' }) },
    { name: 'a start', run: withSources(['pending', 'pending', 'pending', 'pending']) },
    { name: 'boards still waiting', run: withSources(['done', 'failed', 'pending', 'pending']) },
    { name: 'every board failing', run: withSources(['failed', 'failed', 'failed', 'failed']) },
    { name: 'boards read', run: withSources(['done', 'done', 'done', 'failed']) },
    { name: 'ads being checked', run: withSources(['done', 'done', 'done', 'done'], { state: 'active', checked: 3, total: 9, results_by: null }) },
    { name: 'ads checked', run: withSources(['done', 'done', 'done', 'done'], { state: 'done', checked: 9, total: 9, results_by: null }) },
    { name: 'no new ads', run: withSources(['done', 'done', 'done', 'done'], { state: 'done', checked: 0, total: 0, results_by: null }) },
    { name: 'a scheduled check', run: withSources(['done', 'done', 'done', 'done'], { state: 'active', checked: 1, total: 9, results_by: '2026-10-12T05:00:00Z' }) },
  ])('should show the progress of $name without English text', async ({ run }) => {
    renderNorwegian(<RunProgress progress={run} editsDisabled={false} onCancel={vi.fn()} />)

    expect(await screen.findByRole('progressbar', { name: 'Fremdrift for kjøringen' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should show the percent with a space before the sign', async () => {
    renderNorwegian(<RunProgress progress={progress} editsDisabled={false} onCancel={vi.fn()} />)

    expect(await screen.findByText(`${progress.percent} %`)).toBeInTheDocument()
  })

  it('should sum up a finished run without English text', async () => {
    const result = { ...payloadFixtures['run.finished'], failed_sources: ['finn' as const], budget_paused: true }
    renderNorwegian(<RunFinishedBanner view={{ kind: 'finished', result }} onDismiss={vi.fn()} />)

    expect(await screen.findByRole('link', { name: 'Se nye treff' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should join unread sources with "og"', async () => {
    const result = { ...payloadFixtures['run.finished'], failed_sources: ['finn' as const, 'linkedin' as const] }
    renderNorwegian(<RunFinishedBanner view={{ kind: 'finished', result }} onDismiss={vi.fn()} />)

    expect(await screen.findByText('Kunne ikke lese FINN og LinkedIn.')).toBeInTheDocument()
  })

  it('should tell a cancelled run in bokmål', async () => {
    renderNorwegian(<RunFinishedBanner view={{ kind: 'cancelled', runId: progress.run_id }} onDismiss={vi.fn()} />)

    expect(await screen.findByText('Kjøringen ble avbrutt.')).toBeInTheDocument()
    expectNoEnglish()
  })

  it.each([
    { name: 'a running machine', runStatus: status, offline: false, line: 'Kjører nå' },
    {
      name: 'an idle machine',
      runStatus: idle,
      offline: false,
      line: `Leste 812 annonser kl. ${formatClock(LAST_RUN_AT)}, neste kjøring kl. ${formatClock(NEXT_RUN_AT)}`,
    },
    {
      name: 'an idle machine without run times',
      runStatus: { ...idle, next_run_at: null },
      offline: false,
      line: `Leste 812 annonser kl. ${formatClock(LAST_RUN_AT)}, ingen kjøretider satt`,
    },
    { name: 'a machine that has not run', runStatus: { ...idle, last_run: null }, offline: false, line: `Neste kjøring kl. ${formatClock(NEXT_RUN_AT)}` },
    { name: 'a fresh machine', runStatus: payloadFixtures['status.idle'], offline: false, line: 'Ingen kjøretider satt' },
    { name: 'an offline machine', runStatus: status, offline: true, line: 'Maskinen er ikke på nett' },
  ])('should describe $name in bokmål', async ({ runStatus, offline, line }) => {
    renderNorwegian(
      <>
        <RunLine status={runStatus} offline={offline} />
        <RunNowButton running={runStatus.running !== null} editsDisabled={false} onRun={vi.fn()} />
      </>,
    )

    expect(await screen.findByText(line)).toBeInTheDocument()
    expectNoEnglish()
  })
})
