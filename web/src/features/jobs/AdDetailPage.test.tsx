import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { heldSend, sentDrafts, SNAPSHOTS } from '@/test/appData'
import { renderApp } from '@/test/appHarness'

const UNMARKED = SNAPSHOTS.ads.active.find((ad) => ad.mark === null)
const APPLIED = SNAPSHOTS.ads.active.find((ad) => ad.mark === 'applied')
const UNMARKED_ID = UNMARKED?.id ?? ''
const APPLIED_ID = APPLIED?.id ?? ''
const UNMARKED_TITLE = UNMARKED?.title ?? ''

function linkPath(adId: string, action: string): string {
  return `/app/ad?id=${encodeURIComponent(adId)}&action=${action}`
}

describe('AdDetailPage', () => {
  it('should send no ad.mark for an email link before the click', async () => {
    const { send } = renderApp(linkPath(UNMARKED_ID, 'applied'))

    await screen.findByRole('button', { name: 'Mark as applied' })

    expect(sentDrafts(send)).toEqual([])
  })

  it('should send one ad.mark when the email link choice is clicked', async () => {
    const { send, user } = renderApp(linkPath(UNMARKED_ID, 'not_for_me'))

    await user.click(await screen.findByRole('button', { name: 'Mark as not for me' }))

    await screen.findByText('Marked as not for me.')
    expect(sentDrafts(send)).toEqual([{ type: 'ad.mark', v: 1, ad_id: UNMARKED_ID, mark: 'not_for_me' }])
  })

  it('should offer the choice of an email link opened from an ad page', async () => {
    const { router } = renderApp(`/app/ad?id=${encodeURIComponent(APPLIED_ID)}`)
    await screen.findByRole('heading', { name: APPLIED?.title ?? '' })

    await act(() => router.navigate(linkPath(UNMARKED_ID, 'applied')))

    expect(await screen.findByRole('button', { name: 'Mark as applied' })).toBeInTheDocument()
  })

  it('should drop the action from the URL of an email link', async () => {
    const { router } = renderApp(linkPath(UNMARKED_ID, 'applied'))

    await screen.findByRole('button', { name: 'Mark as applied' })

    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(`/app/ad?id=${encodeURIComponent(UNMARKED_ID)}`)
  })

  it('should drop the marked notice when the machine refuses the email link mark', async () => {
    const held = heldSend()
    const { user } = renderApp(linkPath(UNMARKED_ID, 'applied'), { send: held.send })
    await user.click(await screen.findByRole('button', { name: 'Mark as applied' }))

    act(() => held.reject())

    await screen.findByRole('alert')
    expect(screen.queryByText('Marked as applied.')).toBeNull()
  })

  it('should send nothing for an email link the ad already has', async () => {
    const { send } = renderApp(linkPath(APPLIED_ID, 'applied'))

    await screen.findByText('Marked as applied.')

    expect(sentDrafts(send)).toEqual([])
  })

  it('should keep the email link choice off while the machine is offline', async () => {
    renderApp(linkPath(UNMARKED_ID, 'not_for_me'), { state: { kind: 'machineOffline', lastSeen: undefined } })

    expect(await screen.findByRole('button', { name: 'Mark as not for me' })).toBeDisabled()
  })

  it('should undo an email link mark from the notice', async () => {
    const { send, user } = renderApp(linkPath(UNMARKED_ID, 'applied'))
    await user.click(await screen.findByRole('button', { name: 'Mark as applied' }))
    await screen.findByText('Marked as applied.')

    await user.click(screen.getAllByRole('button', { name: 'Undo' })[0] ?? document.body)

    await waitFor(() => expect(sentDrafts(send).at(-1)).toEqual({ type: 'ad.unmark', v: 1, ad_id: UNMARKED_ID }))
  })

  it('should drop the marked notice after Undo', async () => {
    const { user } = renderApp(linkPath(UNMARKED_ID, 'applied'))
    await user.click(await screen.findByRole('button', { name: 'Mark as applied' }))
    await screen.findByText('Marked as applied.')

    await user.click(screen.getAllByRole('button', { name: 'Undo' })[0] ?? document.body)

    await waitFor(() => expect(screen.queryByText('Marked as applied.')).toBeNull())
  })

  it('should say the ad is gone for an id not in the list', async () => {
    renderApp(linkPath('nav-unknown', 'applied'))

    expect(await screen.findByText('This ad is no longer in your list.')).toBeInTheDocument()
  })

  it('should head a missing ad page "Ad not found"', async () => {
    renderApp('/app/ad?id=nav-unknown')

    expect(await screen.findByRole('heading', { level: 1, name: 'Ad not found' })).toBeInTheDocument()
  })

  it('should title a missing ad page as "Ad not found"', async () => {
    renderApp('/app/ad?id=nav-unknown')

    await waitFor(() => expect(document.title).toBe('Ad not found · jobranger'))
  })

  it('should show the detail of the ad in the URL', async () => {
    renderApp(`/app/ad?id=${encodeURIComponent(UNMARKED_ID)}`)

    expect(await screen.findByRole('heading', { level: 1, name: UNMARKED_TITLE })).toBeInTheDocument()
  })

  it('should say nothing about marking for an email link to an ad not in the list', async () => {
    const { router } = renderApp(linkPath('nav-unknown', 'applied'))

    await waitFor(() => expect(router.state.location.search).toBe('?id=nav-unknown'))

    expect(screen.queryByText('Marked as applied.')).toBeNull()
  })

  it('should replace the email link in the history', async () => {
    const { router } = renderApp(linkPath(UNMARKED_ID, 'applied'))

    await screen.findByRole('button', { name: 'Mark as applied' })

    expect(router.state.historyAction).toBe('REPLACE')
  })

  it('should label the role from the machine catalogue', async () => {
    renderApp(`/app/ad?id=${encodeURIComponent(UNMARKED_ID)}`)

    const article = (await screen.findByRole('heading', { level: 1, name: UNMARKED_TITLE })).closest('article') ?? document.body

    expect(within(article).getByText('Data engineering')).toBeInTheDocument()
  })
})
