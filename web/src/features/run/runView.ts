import type {
  RunCancelled,
  RunFinished,
  RunId,
  RunProgress,
  SourceId,
  SourceStep,
  Status,
} from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'

export type RunMessage = RunProgress | RunFinished | RunCancelled

export type RunView =
  | { kind: 'running'; progress: RunProgress }
  | { kind: 'finished'; result: RunFinished }
  | { kind: 'cancelled'; runId: RunId }

export type EndedRun = Exclude<RunView, { kind: 'running' }>

export type StepState = 'pending' | 'active' | 'done' | 'failed' | 'skipped'

interface RunStep {
  key: string
  state: StepState
  text: string
}

export const SOURCE_LABELS: Record<SourceId, string> = {
  nav: 'NAV',
  finn: 'FINN',
  linkedin: 'LinkedIn',
  teamtailor: 'Teamtailor',
  ashby: 'Ashby',
}

const MODEL_STEP_KEY = 'model'

function endedRunId(view: EndedRun): RunId {
  return view.kind === 'finished' ? view.result.run_id : view.runId
}

export function reduceRun(view: RunView | undefined, message: RunMessage): RunView {
  switch (message.type) {
    case 'run.progress':
      if (view !== undefined && view.kind !== 'running' && endedRunId(view) === message.run_id) return view
      return { kind: 'running', progress: message }
    case 'run.finished':
      return { kind: 'finished', result: message }
    case 'run.cancelled':
      return { kind: 'cancelled', runId: message.run_id }
  }
}

export function settleRun(view: RunView | undefined, status: Status): RunView | undefined {
  if (view?.kind !== 'running') return view
  return status.running?.run_id === view.progress.run_id ? view : undefined
}

function sourceStep(step: SourceStep): RunStep {
  const label = SOURCE_LABELS[step.source]
  const texts: Record<SourceStep['state'], string> = {
    pending: label,
    active: `Reading ${label}`,
    done: `${label}, ${step.found_new ?? 0} new`,
    failed: `${label} failed`,
  }
  return { key: step.source, state: step.state, text: texts[step.state] }
}

function modelStep(model: RunProgress['model']): RunStep {
  const texts: Record<RunProgress['model']['state'], string> = {
    pending: 'Check with your AI provider',
    active: `Checking ads ${model.checked} of ${model.total}`,
    done: `Checked ${model.total} ads`,
    skipped: 'Paused at your monthly limit',
  }
  return { key: MODEL_STEP_KEY, state: model.state, text: texts[model.state] }
}

export function runSteps(progress: RunProgress): RunStep[] {
  return [...progress.sources.map(sourceStep), modelStep(progress.model)]
}

function modelTitle(model: RunProgress['model']): string | undefined {
  if (model.results_by !== null) return `A scheduled run is checking ads, results by ${formatClock(model.results_by)}`
  if (model.state === 'done' && model.total === 0) return 'No new ads to check'
  if (model.state === 'active' || model.state === 'done') return `Checking ${model.total} new ads against your CV`
  if (model.state === 'skipped') return 'AI checks paused at your monthly limit'
  return undefined
}

function sourcesTitle(sources: SourceStep[]): string {
  const reading = sources.find((source) => source.state === 'active')
  if (reading !== undefined) return `Reading ${SOURCE_LABELS[reading.source]}`
  const waiting = sources.filter((source) => source.state === 'pending').length
  if (waiting === sources.length) return 'Starting the run'
  if (waiting > 0) return 'Reading job boards'
  if (sources.every((source) => source.state === 'failed')) return 'Could not read any job board'
  return 'Sources read, checking ads next'
}

export function runTitle(progress: RunProgress): string {
  return modelTitle(progress.model) ?? sourcesTitle(progress.sources)
}
