import { act, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { QUERY_KEYS } from '@/app/queryKeys'
import { markAd } from '@/features/jobs/selectors'
import type { AdsSnapshot, ConfigSnapshot } from '@/features/relay/contract/payload.gen'
import { heldSend, sentDrafts, SNAPSHOTS } from '@/test/appData'
import { renderApp } from '@/test/appHarness'

const UNMARKED = SNAPSHOTS.ads.active.find((ad) => ad.mark === null) ?? SNAPSHOTS.ads.active[0]
const UNMARKED_ID = UNMARKED?.id ?? ''
const UNMARKED_TITLE = UNMARKED?.title ?? ''
const APPLIED = SNAPSHOTS.ads.active.find((ad) => ad.mark === 'applied')
const APPLIED_ID = APPLIED?.id ?? ''
const APPLIED_TITLE = APPLIED?.title ?? ''
const HIDDEN = SNAPSHOTS.ads.hidden[0]
const HIDDEN_TITLE = HIDDEN?.title ?? ''
const LATER_SNAPSHOT_MARKED_AT = '2026-10-12T09:00:00Z'
const BROWSER_NOW = new Date('2026-11-30T12:00:00Z')

function cachedAd(queryClient: ReturnType<typeof renderApp>['queryClient'], adId: string) {
  return queryClient.getQueryData<AdsSnapshot>(QUERY_KEYS.ads)?.active.find((ad) => ad.id === adId)
}

function row(title: string): HTMLElement {
  const heading = screen.getByRole('link', { name: title })
  const article = heading.closest('article')
  if (article === null) throw new Error(`no ad row for ${title}`)
  return article
}

afterEach(() => {
  vi.useRealTimers()
})

describe('JobsPage', () => {
  it('should show a mark at once while the machine saves it', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: UNMARKED_TITLE })

    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))

    expect(within(row(UNMARKED_TITLE)).getByText('Skipped')).toBeInTheDocument()
  })

  it('should send ad.mark with the ad and the mark', async () => {
    const { user, send } = renderApp('/app/active')
    await screen.findByRole('link', { name: UNMARKED_TITLE })

    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'I applied' }))

    expect(sentDrafts(send)).toEqual([{ type: 'ad.mark', v: 1, ad_id: UNMARKED_ID, mark: 'applied' }])
  })

  it('should restore the previous mark when the machine refuses it', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: UNMARKED_TITLE })
    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))

    act(() => held.reject())

    expect(await within(row(UNMARKED_TITLE)).findByRole('alert')).toHaveTextContent('Not saved: Run times must be at least 2 hours apart')
    expect(within(row(UNMARKED_TITLE)).queryByText('Skipped')).toBeNull()
  })

  it('should clear the mark and its time at once on Undo', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: APPLIED_TITLE })

    await user.click(within(row(APPLIED_TITLE)).getByRole('button', { name: 'Undo' }))

    expect([cachedAd(queryClient, APPLIED_ID)?.mark, cachedAd(queryClient, APPLIED_ID)?.marked_at]).toEqual([null, null])
  })

  it('should keep the error of another ad when one ad is marked again', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: UNMARKED_TITLE })
    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))
    act(() => held.reject())
    await user.click(within(row(APPLIED_TITLE)).getByRole('button', { name: 'Undo' }))
    act(() => held.reject())
    await within(row(APPLIED_TITLE)).findByRole('alert')

    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))

    expect(within(row(APPLIED_TITLE)).getByRole('alert')).toBeInTheDocument()
  })

  it('should keep a newer snapshot from the machine when an earlier mark fails', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: UNMARKED_TITLE })
    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))
    act(() => queryClient.setQueryData(QUERY_KEYS.ads, markAd(SNAPSHOTS.ads, UNMARKED_ID, 'not_for_me', LATER_SNAPSHOT_MARKED_AT)))

    act(() => held.reject())

    await within(row(UNMARKED_TITLE)).findByRole('alert')
    expect(cachedAd(queryClient, UNMARKED_ID)?.mark).toBe('not_for_me')
  })

  it('should send ad.show_anyway for the picked hidden ad', async () => {
    const { user, send } = renderApp('/app/hidden')

    await user.click(await screen.findByRole('button', { name: 'Show anyway', description: HIDDEN_TITLE }))

    expect([sentDrafts(send), screen.getByRole('button', { name: 'Moved to Active' })]).toEqual([
      [{ type: 'ad.show_anyway', v: 1, ad_id: HIDDEN?.id, on: true }],
      expect.anything(),
    ])
  })

  it('should put the picked hidden reason in the URL', async () => {
    const { user, router } = renderApp('/app/hidden')

    await user.click(await screen.findByRole('button', { name: /^Other reasons/ }))

    expect(router.state.location.search).toBe('?reason=other')
  })

  it('should list only the hidden ads of the reason in the URL', async () => {
    renderApp('/app/hidden?reason=other')

    const table = await screen.findByRole('table', { name: 'Hidden ads' })

    expect(within(table).getAllByRole('row')).toHaveLength(2)
  })

  it('should save a role change from the sidebar as a targets patch', async () => {
    const { user, send, queryClient } = renderApp('/app/today')
    const sidebar = await screen.findByRole('complementary', { name: 'Your search' })

    await user.click(within(sidebar).getByRole('checkbox', { name: 'Frontend' }))

    expect([sentDrafts(send), queryClient.getQueryData<ConfigSnapshot>(QUERY_KEYS.config)?.targets.subfields]).toEqual([
      [{ type: 'config.update', v: 1, patch: { targets: { subfields: ['backend', 'frontend', 'data_engineering'] } } }],
      ['backend', 'frontend', 'data_engineering'],
    ])
  })

  it('should say why a role change from the sidebar was not saved', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/today', { send: held.send })
    const sidebar = await screen.findByRole('complementary', { name: 'Your search' })
    await user.click(within(sidebar).getByRole('checkbox', { name: 'Frontend' }))

    act(() => held.reject())

    expect(await within(sidebar).findByText('Not saved: Run times must be at least 2 hours apart')).toBeInTheDocument()
  })

  it('should count the usage chart from the machine day when the browser clock is later', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(BROWSER_NOW)

    renderApp('/app/today')

    expect(await screen.findByRole('img', { name: 'Today, 210k tokens, $0.41' })).toBeInTheDocument()
  })

  it('should count applications in the Applications tab', async () => {
    renderApp('/app/today')

    const lists = await screen.findByRole('navigation', { name: 'Lists' })

    await waitFor(() => expect(within(lists).getByRole('link', { name: /^Applications/ })).toHaveTextContent(`Applications ${SNAPSHOTS.applications.applications_total}`))
  })

  it('should move a hidden ad back when show anyway fails', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/hidden', { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Show anyway', description: HIDDEN_TITLE }))

    act(() => held.reject())

    expect(await screen.findByText('Not saved: Run times must be at least 2 hours apart')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Show anyway', description: HIDDEN_TITLE })).toBeInTheDocument()
  })

  it('should drop the hidden reason from the URL on All', async () => {
    const { user, router } = renderApp('/app/hidden?reason=other')

    await user.click(await screen.findByRole('button', { name: /^All/ }))

    expect(router.state.location.search).toBe('')
  })

  it('should clear the error of an ad once a later mark is sent', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: UNMARKED_TITLE })
    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))
    act(() => held.reject())
    await within(row(UNMARKED_TITLE)).findByRole('alert')

    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))

    expect(within(row(UNMARKED_TITLE)).queryByRole('alert')).toBeNull()
  })

  it('should keep the mark time the machine sent when an earlier mark fails', async () => {
    const held = heldSend()
    const { user, queryClient } = renderApp('/app/active', { send: held.send })
    await screen.findByRole('link', { name: UNMARKED_TITLE })
    await user.click(within(row(UNMARKED_TITLE)).getByRole('button', { name: 'Skip' }))
    act(() => queryClient.setQueryData(QUERY_KEYS.ads, markAd(SNAPSHOTS.ads, UNMARKED_ID, 'skipped', LATER_SNAPSHOT_MARKED_AT)))

    act(() => held.reject())

    await within(row(UNMARKED_TITLE)).findByRole('alert')
    expect(cachedAd(queryClient, UNMARKED_ID)?.marked_at).toBe(LATER_SNAPSHOT_MARKED_AT)
  })

  it('should clear the sidebar error once a later role change saves', async () => {
    const held = heldSend()
    const { user } = renderApp('/app/today', { send: held.send })
    const sidebar = await screen.findByRole('complementary', { name: 'Your search' })
    await user.click(within(sidebar).getByRole('checkbox', { name: 'Frontend' }))
    act(() => held.reject())
    await within(sidebar).findByText(/^Not saved/)

    await user.click(within(sidebar).getByRole('checkbox', { name: 'Frontend' }))
    act(() => held.resolve())

    await waitFor(() => expect(within(sidebar).queryByText(/^Not saved/)).toBeNull())
  })

  it('should replace the history entry when a hidden reason is picked', async () => {
    const { user, router } = renderApp('/app/hidden')

    await user.click(await screen.findByRole('button', { name: /^Other reasons/ }))

    expect(router.state.historyAction).toBe('REPLACE')
  })

  it('should show no error on Hidden before a show anyway fails', async () => {
    renderApp('/app/hidden')

    await screen.findByRole('table', { name: 'Hidden ads' })

    expect(screen.queryByText(/^Not saved/)).toBeNull()
  })

  it('should name the year limit from the config in the hidden summary', async () => {
    renderApp('/app/today')

    expect(await screen.findByText(/need 3\+ yrs/)).toBeInTheDocument()
  })

  it('should show the applications summary in the sidebar', async () => {
    renderApp('/app/today')

    const sidebar = await screen.findByRole('complementary', { name: 'Your search' })

    expect(within(sidebar).getByRole('heading', { name: 'Applications' })).toBeInTheDocument()
  })
})
