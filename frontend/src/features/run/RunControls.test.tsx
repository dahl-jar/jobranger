import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { QUERY_KEYS } from '@/app/queryKeys'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { RunCancelled, RunFinished, RunProgress, Status } from '@/features/relay/contract/payload.gen'
import type { RunView } from '@/features/run/runView'
import { formatClock } from '@/shared/format/dates'
import { heldSend, refusal, sentDrafts, SNAPSHOTS } from '@/test/appData'
import { renderApp } from '@/test/appHarness'

const PROGRESS = payloadFixtures['run.progress'] as RunProgress
const FINISHED: RunFinished = { ...(payloadFixtures['run.finished'] as RunFinished), failed_sources: [] }
const CANCELLED = payloadFixtures['run.cancelled'] as RunCancelled
const RUNNING_STATUS = payloadFixtures['status.manual-run'] as Status

function withRun(view: RunView) {
  return { ...SNAPSHOTS, status: RUNNING_STATUS, run: view }
}

describe('RunControls', () => {
  it('should send run.start once while the machine answers', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/today', { send: held.send })
    const runNow = await screen.findByRole('button', { name: 'Run now' })

    await user.click(runNow)
    await user.click(runNow)

    expect([sentDrafts(held.send), runNow.hasAttribute('disabled')]).toEqual([[{ type: 'run.start', v: 1 }], true])
  })

  it('should say a run is already going when the machine refuses the start', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/today', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Run now' }))

    act(() => held.reject(refusal('run_in_progress', 'locked')))

    expect(await screen.findByRole('alert')).toHaveTextContent('A run is already going.')
  })

  it('should keep Cancel off while run.cancel is sent', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/today', { data: withRun({ kind: 'running', progress: PROGRESS }), send: held.send })
    const cancel = await screen.findByRole('button', { name: 'Cancel' })

    await user.click(cancel)

    expect([sentDrafts(held.send), cancel.hasAttribute('disabled')]).toEqual([[{ type: 'run.cancel', v: 1, run_id: PROGRESS.run_id }], true])
  })

  it('should announce progress and then the finished run in the same status region', async () => {
    const { queryClient } = renderApp('/app/today', { data: withRun({ kind: 'running', progress: PROGRESS }) })
    const region = await screen.findByText('Reading Teamtailor 40%')
    const finished: RunView = { kind: 'finished', result: FINISHED }

    act(() => queryClient.setQueryData(QUERY_KEYS.run, finished))

    await waitFor(() => expect(region.textContent).not.toBe('Reading Teamtailor 40%'))
    expect([region.getAttribute('role'), region.isConnected, region.textContent]).toEqual([
      'status',
      true,
      `Run finished at ${formatClock(FINISHED.finished_at)}. 37 new ads · 6 matches · 31 hidden · $0.41`,
    ])
  })

  it('should announce a cancelled run', async () => {
    renderApp('/app/today', { data: withRun({ kind: 'cancelled', runId: CANCELLED.run_id }) })

    await waitFor(() => expect(screen.getAllByRole('status').map((status) => status.textContent)).toContain('Run cancelled.'))
  })

  it('should clear the banner on Dismiss', async () => {
    const { user, queryClient } = renderApp('/app/today', { data: withRun({ kind: 'finished', result: FINISHED }) })

    await user.click(await screen.findByRole('button', { name: 'Dismiss' }))

    expect(queryClient.getQueryData(QUERY_KEYS.run)).toBeUndefined()
  })

  it('should show the run line from the machine status', async () => {
    renderApp('/app/today', { data: withRun({ kind: 'running', progress: PROGRESS }) })

    expect(within(await screen.findByRole('banner')).getByText('Running now')).toBeInTheDocument()
  })

  it('should say a scheduled run is checking ads when the machine refuses the start during one', async () => {
    const held = heldSend()
    const scheduled = payloadFixtures.status as Status
    const { user, queryClient } = renderApp('/app/today', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Run now' }))
    act(() => queryClient.setQueryData(QUERY_KEYS.status, scheduled))

    act(() => held.reject(refusal('run_in_progress', 'locked')))

    expect(await screen.findByRole('alert')).toHaveTextContent(`A scheduled run is checking ads, results by ${formatClock(scheduled.running?.results_by ?? '')}.`)
  })

  it('should say why a start was not saved', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/today', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Run now' }))

    act(() => held.reject())

    expect(await screen.findByRole('alert')).toHaveTextContent('Not saved: Run times must be at least 2 hours apart')
  })

  it('should keep Run now off while the machine runs', async () => {
    renderApp('/app/today', { data: { ...SNAPSHOTS, status: RUNNING_STATUS } })

    expect(await within(await screen.findByRole('banner')).findByRole('button', { name: 'Running' })).toBeDisabled()
  })

  it('should show no banner while a run is going', async () => {
    renderApp('/app/today', { data: withRun({ kind: 'running', progress: PROGRESS }) })

    await screen.findByRole('button', { name: 'Cancel' })

    expect(screen.queryByRole('button', { name: 'Dismiss' })).toBeNull()
  })
})
