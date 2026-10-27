import { act, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { QUERY_KEYS } from '@/app/queryKeys'
import { UNDO_WINDOW_MS } from '@/features/applications/useRemoveWithUndo'
import type { ApplicationsSnapshot } from '@/features/relay/contract/payload.gen'
import { heldSend, REFUSED, sentDrafts, SNAPSHOTS } from '@/test/appData'
import { renderApp } from '@/test/appHarness'
import { stubDialogs } from '@/test/dialog'

const LISTED = SNAPSHOTS.applications.applications.find((application) => !application.removed)
const LISTED_ID = LISTED?.id ?? 0
const NOTE = 'Call back on Monday'

function cachedApplication(queryClient: ReturnType<typeof renderApp>['queryClient']) {
  return queryClient.getQueryData<ApplicationsSnapshot>(QUERY_KEYS.applications)?.applications.find((application) => application.id === LISTED_ID)
}

afterEach(() => {
  vi.useRealTimers()
})

describe('ApplicationsScreen', () => {
  stubDialogs()

  it('should send application.create with the typed company', async () => {
    const { user, send } = renderApp('/app/applications')
    await user.click(await screen.findByRole('button', { name: 'Add by hand' }))

    await user.type(screen.getByRole('textbox', { name: 'Company' }), 'Org Four AS')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(sentDrafts(send)).toEqual([{ type: 'application.create', v: 1, company: 'Org Four AS', title: '', note: '' }])
  })

  it('should change a status at once while the machine saves it', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/applications', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Status: Waiting' }))

    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))

    expect([sentDrafts(held.send), cachedApplication(queryClient)?.status]).toEqual([
      [{ type: 'application.update', v: 1, application_id: LISTED_ID, status: 'offer' }],
      'offer',
    ])
  })

  it('should restore the status when the machine refuses it', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/applications', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Status: Waiting' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))

    act(() => held.reject())

    await screen.findByRole('alert')
    expect(cachedApplication(queryClient)?.status).toBe('waiting')
  })

  it('should save a changed note from the panel', async () => {
    const { user, send } = renderApp(`/app/applications/${LISTED_ID}`)
    const note = await screen.findByRole('textbox', { name: 'Note' })

    await user.type(note, NOTE)
    await user.click(screen.getByRole('button', { name: 'Done' }))

    await waitFor(() => expect(sentDrafts(send)).toEqual([{ type: 'application.update', v: 1, application_id: LISTED_ID, note: NOTE }]))
  })

  it('should send application.remove once the undo window ends', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { user, send, queryClient } = renderApp(`/app/applications/${LISTED_ID}`)
    await user.click(await screen.findByRole('button', { name: 'Remove from list' }))
    const beforeWindow = sentDrafts(send).length

    await act(() => vi.advanceTimersByTimeAsync(UNDO_WINDOW_MS))

    expect([beforeWindow, sentDrafts(send), cachedApplication(queryClient)?.removed]).toEqual([
      0,
      [{ type: 'application.remove', v: 1, application_id: LISTED_ID, removed: true }],
      true,
    ])
  })

  it('should bring a removed application back when the machine refuses the removal', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const held = heldSend()
    const { user, queryClient } = renderApp(`/app/applications/${LISTED_ID}`, { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Remove from list' }))
    await act(() => vi.advanceTimersByTimeAsync(UNDO_WINDOW_MS))

    act(() => held.reject())

    await screen.findByRole('alert')
    expect(cachedApplication(queryClient)?.removed).toBe(false)
  })

  it('should show the machine reason when adding an application is refused', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/applications', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Add by hand' }))
    await user.type(screen.getByRole('textbox', { name: 'Company' }), 'Org Four AS')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    act(() => held.reject())

    expect(await screen.findByRole('alert')).toHaveTextContent(REFUSED.message)
  })

  it('should go back to the list for an application that is not in it', async () => {
    const { router } = renderApp('/app/applications/999?status=waiting')

    await waitFor(() => expect(`${router.state.location.pathname}${router.state.location.search}`).toBe('/app/applications?status=waiting'))
  })

  it('should show the applications list with the job tabs', async () => {
    renderApp('/app/applications')

    const lists = await screen.findByRole('navigation', { name: 'Lists' })

    expect(within(lists).getByRole('link', { name: /^Applications/ })).toHaveAttribute('aria-current', 'page')
  })

  it('should keep a newer status from the machine when an earlier change fails', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/applications', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Status: Waiting' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))
    act(() => queryClient.setQueryData<ApplicationsSnapshot>(QUERY_KEYS.applications, (snapshot) => snapshot && { ...snapshot, applications: snapshot.applications.map((application) => (application.id === LISTED_ID ? { ...application, status: 'rejected' } : application)) }))

    act(() => held.reject())

    await screen.findByRole('alert')
    expect(cachedApplication(queryClient)?.status).toBe('rejected')
  })

  it('should clear the error once a later change is sent', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/applications', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Status: Waiting' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))
    act(() => held.reject())
    await screen.findByRole('alert')

    await user.click(screen.getByRole('button', { name: 'Status: Waiting' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Interview' }))

    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('should count applications in the tab on the applications page', async () => {
    renderApp('/app/applications')

    const lists = await screen.findByRole('navigation', { name: 'Lists' })

    expect(within(lists).getByRole('link', { name: /^Applications/ })).toHaveTextContent(`Applications ${SNAPSHOTS.applications.applications_total}`)
  })

  it('should write only the note into the cache when a note saves', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp(`/app/applications/${LISTED_ID}`, { send: held.send })
    const note = await screen.findByRole('textbox', { name: 'Note' })

    await user.type(note, NOTE)
    await user.click(screen.getByRole('button', { name: 'Done' }))

    await waitFor(() => expect([cachedApplication(queryClient)?.note, cachedApplication(queryClient)?.status]).toEqual([NOTE, LISTED?.status]))
  })
})
