import { act, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { QUERY_KEYS } from '@/app/queryKeys'
import { savePendingDeepLink, takePendingDeepLink } from '@/features/jobs/deepLink'
import { deepLinkPath, detailPath } from '@/features/jobs/paths'
import { SNAPSHOTS } from '@/test/appData'
import { renderApp } from '@/test/appHarness'
import { expectNoEnglish } from '@/test/english'
import { LANGUAGE_STORAGE_KEY } from '@/shared/i18n/language'

const UNMARKED_ID = SNAPSHOTS.ads.active.find((ad) => ad.mark === null)?.id ?? ''
const LINK_PATH = deepLinkPath({ adId: UNMARKED_ID, action: 'applied' })
const SHOWN_DATA = [
  ...SNAPSHOTS.ads.active.flatMap((ad) => [ad.company, ad.title, ad.place, ad.reason, ad.summary, ...ad.gaps, ...ad.checks.map((check) => check.text)]),
  ...SNAPSHOTS.config.roles.map((role) => role.label),
  ...SNAPSHOTS.config.targets.places,
]

describe('AppShell', () => {
  it('should open the today list at /app', async () => {
    const { router } = renderApp('/app')

    await waitFor(() => expect(router.state.location.pathname).toBe('/app/today'))
  })

  it('should send an unknown list to today', async () => {
    const { router } = renderApp('/app/nowhere')

    await waitFor(() => expect(router.state.location.pathname).toBe('/app/today'))
  })

  it('should mark Settings as the current page under settings', async () => {
    renderApp('/app/settings/search')

    const header = await screen.findByRole('banner')

    expect(within(header).getByRole('link', { name: 'Settings' })).toHaveAttribute('aria-current', 'page')
  })

  it('should say the machine is offline in the header', async () => {
    renderApp('/app/today', { state: { kind: 'machineOffline', lastSeen: undefined } })

    const header = await screen.findByRole('banner')

    expect(within(header).getByText('Machine offline')).toBeInTheDocument()
  })

  it('should keep an email link for after pairing when the browser is unpaired', async () => {
    renderApp(LINK_PATH, { state: { kind: 'unpaired', reason: undefined } })

    await waitFor(() => expect(takePendingDeepLink()).toBe(LINK_PATH))
  })

  it('should keep an email link once a loading browser turns out unpaired', async () => {
    const { store } = renderApp(LINK_PATH, { state: { kind: 'loading' } })
    await screen.findByRole('banner')

    act(() => store.dispatch({ kind: 'unpaired', reason: undefined }))

    await waitFor(() => expect(takePendingDeepLink()).toBe(LINK_PATH))
  })

  it('should open a kept email link in place of the start page', async () => {
    savePendingDeepLink(LINK_PATH)
    const { router } = renderApp('/app/today')
    await screen.findByRole('button', { name: 'Mark as applied' })

    await act(() => router.navigate(-1))

    expect(router.state.location.pathname).toBe('/app/ad')
  })

  it('should open a kept email link once the app opens paired', async () => {
    savePendingDeepLink(LINK_PATH)
    const { router } = renderApp('/app/today')

    await waitFor(() => expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(detailPath(UNMARKED_ID)))
  })

  it('should follow the language the machine sends', async () => {
    const { queryClient } = renderApp('/app/today')
    await screen.findByRole('banner')

    act(() => queryClient.setQueryData(QUERY_KEYS.config, { ...SNAPSHOTS.config, language: 'nb' }))

    expect(await within(screen.getByRole('banner')).findByRole('link', { name: 'Innstillinger' })).toBeInTheDocument()
  })

  it.each([
    { path: '/app/today', heading: 'Today' },
    { path: '/app/hidden', heading: 'Hidden' },
    { path: '/app/applications', heading: 'Applications' },
    { path: '/app/settings/search', heading: 'Settings' },
  ])('should give $path the page heading $heading', async ({ path, heading }) => {
    renderApp(path)

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })

  it('should keep what the page shows when the machine comes back online', async () => {
    const { user, store } = renderApp('/app/applications', { state: { kind: 'machineOffline', lastSeen: undefined } })
    await user.click(await screen.findByRole('button', { name: 'Add by hand' }))

    act(() => store.dispatch({ kind: 'ready' }))

    expect(screen.getByRole('form', { name: 'Add an application' })).toBeInTheDocument()
  })

  it('should keep no email link while the browser is paired', async () => {
    renderApp(LINK_PATH)

    await screen.findByRole('button', { name: 'Mark as applied' })

    expect(takePendingDeepLink()).toBeUndefined()
  })

  it.each([
    { path: '/app/active', title: 'Active · jobranger' },
    { path: '/app/applications', title: 'Applications · jobranger' },
    { path: '/app/settings/search', title: 'Settings · jobranger' },
    { path: `/app/ad?id=${encodeURIComponent(SNAPSHOTS.ads.active[0]?.id ?? '')}`, title: `${SNAPSHOTS.ads.active[0]?.title ?? ''} · jobranger` },
  ])('should title $path as "$title"', async ({ path, title }) => {
    renderApp(path)

    await waitFor(() => expect(document.title).toBe(title))
  })

  it('should show the read-only app header and notice in bokmål', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'nb')
    renderApp('/app/today', { state: { kind: 'machineOffline', lastSeen: undefined }, data: { ...SNAPSHOTS, config: { ...SNAPSHOTS.config, language: 'nb' } } })

    await screen.findByRole('heading', { level: 1, name: 'I dag' })

    expectNoEnglish(SHOWN_DATA)
  })
})
