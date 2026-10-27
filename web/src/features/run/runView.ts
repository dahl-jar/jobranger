import type {
  RunCancelled,
  RunFinished,
  RunId,
  RunProgress,
  SourceStep,
  Status,
} from '@/features/relay/contract/payload.gen'
import { formatClock } from '@/shared/format/dates'
import { en, type Messages } from '@/shared/i18n/en'
import { SOURCE_NAMES } from '@/shared/sources'

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

function sourceStep(step: SourceStep, text: Messages): RunStep {
  const label = SOURCE_NAMES[step.source]
  const words = text.run.steps
  const texts: Record<SourceStep['state'], string> = {
    pending: label,
    active: words.reading(label),
    done: words.foundNew(label, step.found_new ?? 0),
    failed: words.failed(label),
  }
  return { key: step.source, state: step.state, text: texts[step.state] }
}

function modelStep(model: RunProgress['model'], text: Messages): RunStep {
  const words = text.run.steps
  const texts: Record<RunProgress['model']['state'], string> = {
    pending: words.modelPending,
    active: words.modelActive(model.checked, model.total),
    done: words.modelDone(model.total),
    skipped: words.modelSkipped,
  }
  return { key: MODEL_STEP_KEY, state: model.state, text: texts[model.state] }
}

export function runSteps(progress: RunProgress, text: Messages = en): RunStep[] {
  return [...progress.sources.map((step) => sourceStep(step, text)), modelStep(progress.model, text)]
}

function modelTitle(model: RunProgress['model'], text: Messages): string | undefined {
  const titles = text.run.titles
  if (model.results_by !== null) return titles.scheduled(formatClock(model.results_by))
  if (model.state === 'done' && model.total === 0) return titles.nothingNew
  if (model.state === 'active' || model.state === 'done') return titles.checking(model.total)
  if (model.state === 'skipped') return titles.paused
  return undefined
}

function sourcesTitle(sources: SourceStep[], text: Messages): string {
  const titles = text.run.titles
  const reading = sources.find((source) => source.state === 'active')
  if (reading !== undefined) return titles.reading(SOURCE_NAMES[reading.source])
  const waiting = sources.filter((source) => source.state === 'pending').length
  if (waiting === sources.length) return titles.starting
  if (waiting > 0) return titles.readingBoards
  if (sources.every((source) => source.state === 'failed')) return titles.allFailed
  return titles.sourcesRead
}

export function runTitle(progress: RunProgress, text: Messages = en): string {
  return modelTitle(progress.model, text) ?? sourcesTitle(progress.sources, text)
}
