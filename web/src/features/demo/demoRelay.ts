import type { QueryClient } from '@tanstack/react-query'
import { QUERY_KEYS } from '@/app/queryKeys'
import type { Snapshots } from '@/app/useSnapshot'
import { demoClock } from '@/features/demo/demoClock'
import { DEMO_TODAY } from '@/features/demo/demoSnapshots'
import type { CommandDraft } from '@/features/relay/commands'
import { createConnectionStore } from '@/features/relay/connectionStore'
import { CONTRACT_VERSION } from '@/features/relay/contract/limits'
import type { ProfileSnapshot, RunFinished, Saved } from '@/features/relay/contract/payload.gen'
import type { Relay } from '@/features/relay/relayContext'
import type { RunView } from '@/features/run/runView'
import type { Clock } from '@/shared/clock'

const SENT_IN_ONE_PART = 1
const DEMO_RUN = { runId: 2, adsRead: 812, newAds: 64, matches: 3, costUsd: 0.1 }

type Change<Name extends keyof Snapshots> = (snapshot: Snapshots[Name]) => Snapshots[Name]
type DraftOf<Type extends CommandDraft['type']> = Extract<CommandDraft, { type: Type }>
type DemoHandler<Type extends CommandDraft['type']> = (client: QueryClient, draft: DraftOf<Type>, now: string) => void
type DemoHandlers = { [Type in CommandDraft['type']]?: DemoHandler<Type> }

function change<Name extends keyof Snapshots>(client: QueryClient, name: Name, update: Change<Name>): void {
  client.setQueryData<Snapshots[Name]>(QUERY_KEYS[name], (snapshot) => snapshot && update(snapshot))
}

function finishedRun(finishedAt: string): RunFinished {
  return {
    type: 'run.finished',
    v: CONTRACT_VERSION,
    run_id: DEMO_RUN.runId,
    finished_at: finishedAt,
    ads_read: DEMO_RUN.adsRead,
    new_ads: DEMO_RUN.newAds,
    matches: DEMO_RUN.matches,
    hidden: DEMO_RUN.newAds - DEMO_RUN.matches,
    cost_usd: DEMO_RUN.costUsd,
    failed_sources: [],
    budget_paused: false,
  }
}

function startRun(client: QueryClient, _draft: DraftOf<'run.start'>, now: string): void {
  const result = finishedRun(now)
  const lastRun = { run_id: result.run_id, finished_at: result.finished_at, ads_read: result.ads_read, matches: result.matches, cost_usd: result.cost_usd }
  const run: RunView = { kind: 'finished', result }
  client.setQueryData(QUERY_KEYS.run, run)
  change(client, 'status', (status) => ({ ...status, last_run: lastRun }))
}

function nextId(ids: readonly number[]): number {
  return Math.max(0, ...ids) + 1
}

function createApplication(client: QueryClient, draft: DraftOf<'application.create'>, now: string): void {
  change(client, 'applications', (snapshot) => {
    const created = {
      id: nextId(snapshot.applications.map((application) => application.id)),
      ad_id: null,
      company: draft.company,
      title: draft.title,
      place: '',
      role_type: null,
      source: null,
      url: null,
      deadline: null,
      fit: null,
      status: 'waiting' as const,
      note: draft.note,
      applied_at: now,
      found_at: null,
      cv_name: null,
      checks: [],
      history: [],
      removed: false,
    }
    return { ...snapshot, applications: [created, ...snapshot.applications], applications_total: snapshot.applications_total + 1 }
  })
}

function draftProjects(draft: DraftOf<'profile.update'>): ProfileSnapshot['projects'] | undefined {
  return draft.projects?.map((project) => ({ id: project.id ?? crypto.randomUUID(), name: project.name }))
}

function updateProfile(client: QueryClient, draft: DraftOf<'profile.update'>): void {
  change(client, 'profile', (profile) => {
    const { summary = profile.summary, education = profile.education, languages = profile.languages, stack = profile.stack } = draft
    return { ...profile, summary, education, languages, stack, projects: draftProjects(draft) ?? profile.projects }
  })
}

function addDocument(client: QueryClient, draft: DraftOf<'context.upload'>): void {
  change(client, 'context', (context) => ({
    ...context,
    docs: [...context.docs, { id: crypto.randomUUID(), name: draft.name, chars: atob(draft.content_b64).length, added_at: DEMO_TODAY }],
  }))
}

const DEMO_HANDLERS: DemoHandlers = {
  'application.create': createApplication,
  'model.select': (client, draft) => change(client, 'config', (config) => ({ ...config, model: { ...config.model, provider: draft.provider, model: draft.model } })),
  'guidance.update': (client, draft) => change(client, 'guidance', (guidance) => ({ ...guidance, text: draft.text, is_default: false })),
  'guidance.reset': (client) => change(client, 'guidance', (guidance) => ({ ...guidance, text: '', is_default: true })),
  'context.update': (client, draft) => change(client, 'context', (context) => ({ ...context, about: draft.about })),
  'context.upload': addDocument,
  'context.remove': (client, draft) => change(client, 'context', (context) => ({ ...context, docs: context.docs.filter((doc) => doc.id !== draft.doc_id) })),
  'cv.upload': (client, draft, now) => change(client, 'profile', (profile) => ({ ...profile, cv_name: draft.name, cv_read_at: now })),
  'profile.update': updateProfile,
  'run.start': startRun,
}

function applyDemoCommand(client: QueryClient, draft: CommandDraft, clock: Clock): void {
  const handler = DEMO_HANDLERS[draft.type] as DemoHandler<CommandDraft['type']> | undefined
  handler?.(client, draft, clock().toISOString())
}

export function createDemoRelay(client: QueryClient, clock: Clock = demoClock()): Relay {
  const store = createConnectionStore()
  store.dispatch({ kind: 'ready' })
  return {
    store,
    send: async (draft, onProgress) => {
      applyDemoCommand(client, draft, clock)
      onProgress?.(SENT_IN_ONE_PART, SENT_IN_ONE_PART)
      const saved: Saved = { type: 'saved', v: CONTRACT_VERSION, ref: crypto.randomUUID(), applies_from: 'now' }
      return saved
    },
    unpair: async () => store.dispatch({ kind: 'unpaired', reason: undefined }),
    retryNow: () => undefined,
  }
}
