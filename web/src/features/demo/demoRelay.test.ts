import { describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/queryClient'
import { QUERY_KEYS } from '@/app/queryKeys'
import { seedSnapshots, type Snapshots } from '@/app/useSnapshot'
import { createDemoRelay } from '@/features/demo/demoRelay'
import { DEMO_TODAY, demoSnapshots } from '@/features/demo/demoSnapshots'
import type { CommandDraft } from '@/features/relay/commands'
import type { RunView } from '@/features/run/runView'

const NOTE_TEXT = 'notes'
const SAMPLE = demoSnapshots('en')
const DAY_TEXT_LENGTH = 10
const FIRST_DOC = { id: 'doc-one', name: 'notes.md', chars: NOTE_TEXT.length, added_at: '2026-10-08' }
const SECOND_DOC = { ...FIRST_DOC, id: 'doc-two', name: 'more.md' }
const PROJECT = { id: 'project-one', name: 'Project one' }
const RUN_FINISHED_AT = '2026-10-08T12:00:00.000Z'

function seeded() {
  const client = createQueryClient()
  seedSnapshots(client, {
    ...SAMPLE,
    context: { ...SAMPLE.context, docs: [FIRST_DOC, SECOND_DOC] },
    guidance: { ...SAMPLE.guidance, text: 'Prefer Oslo.', is_default: false },
  })
  return client
}

function read<Name extends keyof Snapshots>(client: ReturnType<typeof seeded>, name: Name): Snapshots[Name] | undefined {
  return client.getQueryData<Snapshots[Name]>(QUERY_KEYS[name])
}

describe('createDemoRelay', () => {
  it.each<{ name: string; draft: CommandDraft; shows: (client: ReturnType<typeof seeded>) => unknown; expected: unknown }>([
    {
      name: 'a model pick',
      draft: { type: 'model.select', v: 1, provider: 'ollama', model: 'local-model' },
      shows: (client) => read(client, 'config')?.model.model,
      expected: 'local-model',
    },
    { name: 'saved guidance', draft: { type: 'guidance.update', v: 1, text: 'Prefer Bergen.' }, shows: (client) => read(client, 'guidance'), expected: expect.objectContaining({ text: 'Prefer Bergen.', is_default: false }) },
    { name: 'reset guidance', draft: { type: 'guidance.reset', v: 1 }, shows: (client) => read(client, 'guidance'), expected: expect.objectContaining({ text: '', is_default: true }) },
    { name: 'about text', draft: { type: 'context.update', v: 1, about: 'Moving soon.' }, shows: (client) => read(client, 'context')?.about, expected: 'Moving soon.' },
    {
      name: 'an added document',
      draft: { type: 'context.upload', v: 1, name: 'plan.md', content_b64: btoa(NOTE_TEXT) },
      shows: (client) => read(client, 'context')?.docs.map((doc) => [doc.name, doc.chars]),
      expected: [
        [FIRST_DOC.name, FIRST_DOC.chars],
        [SECOND_DOC.name, SECOND_DOC.chars],
        ['plan.md', NOTE_TEXT.length],
      ],
    },
    { name: 'a removed document', draft: { type: 'context.remove', v: 1, doc_id: FIRST_DOC.id }, shows: (client) => read(client, 'context')?.docs, expected: [SECOND_DOC] },
    { name: 'a new CV', draft: { type: 'cv.upload', v: 1, name: 'new-cv.pdf', size_bytes: 1, content_b64: 'AA' }, shows: (client) => read(client, 'profile')?.cv_name, expected: 'new-cv.pdf' },
    {
      name: 'profile edits',
      draft: { type: 'profile.update', v: 1, summary: 'Backend work.', projects: [{ id: null, name: 'New project' }] },
      shows: (client) => {
        const profile = read(client, 'profile')
        return [profile?.summary, profile?.education, profile?.projects.map((project) => project.name)]
      },
      expected: ['Backend work.', SAMPLE.profile.education, ['New project']],
    },
    {
      name: 'profile edits without projects',
      draft: { type: 'profile.update', v: 1, summary: 'Backend work.' },
      shows: (client) => read(client, 'profile')?.projects,
      expected: SAMPLE.profile.projects,
    },
    {
      name: 'a kept project id',
      draft: { type: 'profile.update', v: 1, projects: [PROJECT] },
      shows: (client) => read(client, 'profile')?.projects,
      expected: [PROJECT],
    },
    {
      name: 'a hand-added application',
      draft: { type: 'application.create', v: 1, company: 'Org Four AS', title: 'Developer', note: 'Sent by mail' },
      shows: (client) => read(client, 'applications')?.applications[0],
      expected: expect.objectContaining({ company: 'Org Four AS', title: 'Developer', note: 'Sent by mail', place: '', status: 'waiting', checks: [], history: [], removed: false }),
    },
    {
      name: 'a run',
      draft: { type: 'run.start', v: 1 },
      shows: (client) => client.getQueryData<RunView>(QUERY_KEYS.run),
      expected: expect.objectContaining({ kind: 'finished', result: expect.objectContaining({ new_ads: 64, matches: 3, hidden: 61, failed_sources: [], budget_paused: false }) }),
    },
  ])('should show $name in the sample data', async ({ draft, shows, expected }) => {
    const client = seeded()

    await createDemoRelay(client).send(draft)

    expect(shows(client)).toEqual(expected)
  })

  it.each<{ name: string; draft: CommandDraft; stamp: (client: ReturnType<typeof seeded>) => string | undefined }>([
    {
      name: 'a hand-added application',
      draft: { type: 'application.create', v: 1, company: 'Org Four AS', title: '', note: '' },
      stamp: (client) => read(client, 'applications')?.applications[0]?.applied_at,
    },
    {
      name: 'a finished run',
      draft: { type: 'run.start', v: 1 },
      stamp: (client) => {
        const run = client.getQueryData<RunView>(QUERY_KEYS.run)
        return run?.kind === 'finished' ? run.result.finished_at : undefined
      },
    },
  ])('should date $name by the demo day', async ({ draft, stamp }) => {
    const client = seeded()

    await createDemoRelay(client).send(draft)

    expect(stamp(client)?.slice(0, DAY_TEXT_LENGTH)).toBe(DEMO_TODAY)
  })

  it('should put the finished demo run in the status', async () => {
    const client = seeded()

    await createDemoRelay(client, () => new Date(RUN_FINISHED_AT)).send({ type: 'run.start', v: 1 })

    expect(read(client, 'status')?.last_run).toEqual({ run_id: 2, finished_at: RUN_FINISHED_AT, ads_read: 812, matches: 3, cost_usd: 0.1 })
  })

  it('should number a hand-added application after the highest one', async () => {
    const client = seeded()

    await createDemoRelay(client).send({ type: 'application.create', v: 1, company: 'Org Four AS', title: '', note: '' })

    expect([read(client, 'applications')?.applications[0]?.id, read(client, 'applications')?.applications_total]).toEqual([3, 3])
  })

  it('should report a single sent part', async () => {
    const onProgress = vi.fn()

    await createDemoRelay(seeded()).send({ type: 'run.start', v: 1 }, onProgress)

    expect(onProgress).toHaveBeenCalledExactlyOnceWith(1, 1)
  })

  it('should leave the demo unpaired on Unpair', async () => {
    const relay = createDemoRelay(seeded())

    await relay.unpair()

    expect(relay.store.getSnapshot()).toEqual({ kind: 'unpaired', reason: undefined })
  })
})
