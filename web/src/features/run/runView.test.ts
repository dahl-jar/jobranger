import { describe, expect, it } from 'vitest'
import { reduceRun, runSteps, runTitle, settleRun, type RunView } from '@/features/run/runView'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { RunProgress, SourceStep } from '@/features/relay/contract/payload.gen'

const progress = payloadFixtures['run.progress']
const batch = payloadFixtures['run.progress.batch']
const finished = payloadFixtures['run.finished']
const cancelled = payloadFixtures['run.cancelled']
const status = payloadFixtures['status.manual-run']

const running: RunView = { kind: 'running', progress }
const finishedView: RunView = { kind: 'finished', result: finished }

const FAILED_FINN: SourceStep = { source: 'finn', state: 'failed', found_new: null }
const DONE_FINN: SourceStep = { source: 'finn', state: 'done', found_new: 25 }

function allSources(state: SourceStep['state']): SourceStep[] {
  return progress.sources.map((source) => ({ ...source, state }))
}

function withModel(model: Partial<RunProgress['model']>): RunProgress {
  return { ...progress, model: { ...progress.model, ...model } }
}

describe('reduceRun', () => {
  it('should open a run view on the first progress', () => {
    expect(reduceRun(undefined, progress)).toEqual({ kind: 'running', progress })
  })

  it('should show the latest progress of a running run', () => {
    const next = { ...progress, percent: 55 }

    expect(reduceRun(running, next)).toEqual({ kind: 'running', progress: next })
  })

  it('should keep the finished banner when late progress of the same run arrives', () => {
    expect(reduceRun(finishedView, progress)).toBe(finishedView)
  })

  it('should keep the cancelled banner when late progress of the same run arrives', () => {
    const cancelledView: RunView = { kind: 'cancelled', runId: 42 }

    expect(reduceRun(cancelledView, progress)).toBe(cancelledView)
  })

  it('should start a new run view when progress of another run arrives after a finish', () => {
    const nextRun = { ...progress, run_id: 43 }

    expect(reduceRun(finishedView, nextRun)).toEqual({ kind: 'running', progress: nextRun })
  })

  it.each([
    ['run.finished', finished, { kind: 'finished', result: finished }],
    ['run.cancelled', cancelled, { kind: 'cancelled', runId: 42 }],
  ] as const)('should end the run view on %s', (_, message, expected) => {
    expect(reduceRun(running, message)).toEqual(expected)
  })
})

describe('settleRun', () => {
  it('should leave no view when nothing ran', () => {
    expect(settleRun(undefined, status)).toBeUndefined()
  })

  it('should clear a running view when status has no running run', () => {
    expect(settleRun(running, { ...status, running: null })).toBeUndefined()
  })

  it('should clear a running view when status reports another run', () => {
    const otherRun = { ...status.running, run_id: 43 }

    expect(settleRun(running, { ...status, running: otherRun })).toBeUndefined()
  })

  it('should keep the running view while status reports the same run', () => {
    expect(settleRun(running, status)).toBe(running)
  })

  it('should keep the finished banner when status has no running run', () => {
    expect(settleRun(finishedView, { ...status, running: null })).toBe(finishedView)
  })
})

describe('runSteps', () => {
  it('should describe each source by its state', () => {
    const texts = runSteps(progress).map((step) => step.text)

    expect(texts.slice(0, 3)).toEqual(['NAV, 12 new', 'Reading Teamtailor', 'FINN'])
  })

  it('should say a source failed', () => {
    const [, finn] = runSteps(batch)

    expect(finn).toEqual({ key: 'finn', state: 'failed', text: 'FINN failed' })
  })

  it('should count zero new ads when a done source reports none', () => {
    const run: RunProgress = { ...progress, sources: [{ source: 'nav', state: 'done', found_new: null }] }

    const [nav] = runSteps(run)

    expect(nav?.text).toBe('NAV, 0 new')
  })

  it.each([
    ['pending', withModel({ state: 'pending' }), 'Check with your AI provider'],
    ['active', withModel({ state: 'active', checked: 10, total: 37 }), 'Checking ads 10 of 37'],
    ['done', withModel({ state: 'done', checked: 37, total: 37 }), 'Checked 37 ads'],
    ['skipped', withModel({ state: 'skipped' }), 'Paused at your monthly limit'],
  ])('should describe the model step when %s', (_, run, text) => {
    const modelStep = runSteps(run).at(-1)

    expect(modelStep?.text).toBe(text)
  })
})

describe('runTitle', () => {
  const resultsBy = new Date(2026, 9, 12, 13, 20).toISOString()

  it.each([
    ['a batch is polled', withModel({ state: 'active', results_by: resultsBy }), 'A scheduled run is checking ads, results by 13:20'],
    ['the model checks ads', withModel({ state: 'active', total: 37 }), 'Checking 37 new ads against your CV'],
    ['the model is done', withModel({ state: 'done', total: 37 }), 'Checking 37 new ads against your CV'],
    ['the run found no new ads to check', withModel({ state: 'done', checked: 0, total: 0 }), 'No new ads to check'],
    ['a source is read', progress, 'Reading Teamtailor'],
    ['nothing has started', { ...progress, sources: [] }, 'Starting the run'],
    ['every source is waiting', { ...progress, sources: allSources('pending') }, 'Starting the run'],
    ['the model step is skipped', { ...withModel({ state: 'skipped' }), sources: allSources('done') }, 'AI checks paused at your monthly limit'],
    ['some sources wait between reads', { ...progress, sources: [...allSources('pending'), DONE_FINN] }, 'Reading job boards'],
    ['every source failed', { ...progress, sources: allSources('failed') }, 'Could not read any job board'],
    ['the sources are read and the model waits', { ...progress, sources: [...allSources('done'), FAILED_FINN] }, 'Sources read, checking ads next'],
  ])('should name the current stage when %s', (_, run, title) => {
    expect(runTitle(run)).toBe(title)
  })
})
