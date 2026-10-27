import { act, screen } from '@testing-library/react'
import type { RouteObject } from 'react-router'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { PublicLayout, type PublicLayoutHandle } from '@/app/PublicLayout'
import styles from '@/app/PublicLayout.module.css'
import { renderRoutes } from '@/test/render'
import { blockStorage } from '@/test/stubs'

const PATH = '/page'
const SUBTITLE = 'Setup walkthrough'
const PAGE: RouteObject = { path: PATH, handle: { subtitle: SUBTITLE } satisfies PublicLayoutHandle, Component: () => null }

function renderLayout(page: RouteObject) {
  return renderRoutes([{ Component: PublicLayout, children: [page] }], PATH)
}

const BUSY_DELAY_MS = 300

async function startSlowNavigation() {
  let finish: () => void = () => undefined
  const slow: RouteObject = {
    path: '/slow',
    lazy: () => new Promise((resolve) => {
      finish = () => resolve({ Component: () => <h1>Slow page</h1> })
    }),
  }
  const { router } = renderRoutes([{ Component: PublicLayout, children: [PAGE, slow] }], PATH)
  const page = (await screen.findByRole('banner')).parentElement
  vi.useFakeTimers()
  onTestFinished(() => {
    vi.useRealTimers()
  })
  await act(async () => {
    void router.navigate('/slow')
  })
  return { page, finish: () => finish() }
}

async function pageClasses() {
  return [...((await screen.findByRole('banner')).parentElement?.classList ?? [])]
}

describe('PublicLayout', () => {
  it('should show the subtitle from the route handle', async () => {
    renderLayout(PAGE)

    expect(await screen.findByText(SUBTITLE)).toBeInTheDocument()
  })

  it('should link Home to the landing page when the route asks for it', async () => {
    renderLayout({ ...PAGE, handle: { showHome: true } satisfies PublicLayoutHandle })

    expect(await screen.findByRole('link', { name: 'Home' })).toHaveAttribute('href', '/')
  })

  it('should show the subtitle when the layout sits under the root route', async () => {
    renderRoutes([{ children: [{ Component: PublicLayout, children: [PAGE] }] }], PATH)

    expect(await screen.findByText(SUBTITLE)).toBeInTheDocument()
  })

  it.each([undefined, null])('should leave out Home when the route handle is %s', async (handle) => {
    renderLayout({ ...PAGE, handle })

    await screen.findByRole('link', { name: 'jobranger' })
    expect(screen.queryByRole('link', { name: 'Home' })).not.toBeInTheDocument()
  })

  it('should leave out a subtitle that is not text', async () => {
    renderLayout({ ...PAGE, handle: { subtitle: 42 } })

    await screen.findByRole('link', { name: 'jobranger' })
    expect(screen.queryByText('42')).not.toBeInTheDocument()
  })

  it('should show the header nav from the route handle', async () => {
    renderLayout({ ...PAGE, handle: { nav: <a href="#how">How it works</a> } satisfies PublicLayoutHandle })

    expect(await screen.findByRole('link', { name: 'How it works' })).toHaveAttribute('href', '#how')
  })

  it('should show the header action from the route handle', async () => {
    renderLayout({ ...PAGE, handle: { action: <a href="/app">Open the app</a> } satisfies PublicLayoutHandle })

    expect(await screen.findByRole('link', { name: 'Open the app' })).toHaveAttribute('href', '/app')
  })

  it('should leave out a nav or action that is not an element', async () => {
    renderLayout({ ...PAGE, handle: { nav: 'How it works', action: 'Open the app' } })

    await screen.findByRole('link', { name: 'jobranger' })
    expect(screen.queryByText('How it works')).not.toBeInTheDocument()
    expect(screen.queryByText('Open the app')).not.toBeInTheDocument()
  })

  it('should leave out Home when the route asks for it with text', async () => {
    renderLayout({ ...PAGE, handle: { showHome: 'yes' } })

    await screen.findByRole('link', { name: 'jobranger' })
    expect(screen.queryByRole('link', { name: 'Home' })).not.toBeInTheDocument()
  })

  it('should widen the page when the route asks for it', async () => {
    renderLayout({ ...PAGE, handle: { wide: true } satisfies PublicLayoutHandle })

    expect(await pageClasses()).toContain(styles.wide)
  })

  it('should keep the page narrow when the route sets wide to text', async () => {
    renderLayout({ ...PAGE, handle: { wide: 'yes' } })

    expect(await pageClasses()).not.toContain(styles.wide)
  })

  it('should open a page that has no heading to focus', async () => {
    const other: RouteObject = { path: '/other', handle: { subtitle: 'Other page' } satisfies PublicLayoutHandle, Component: () => null }
    const { router } = renderRoutes([{ Component: PublicLayout, children: [PAGE, other] }], PATH)
    await screen.findByText(SUBTITLE)

    await act(() => router.navigate('/other'))

    expect(await screen.findByText('Other page')).toBeInTheDocument()
  })

  it('should show loading on a page that takes long to open', async () => {
    const { page } = await startSlowNavigation()

    await act(() => vi.advanceTimersByTimeAsync(BUSY_DELAY_MS))

    expect(screen.getByRole('status')).toHaveTextContent('Loading...')
    expect(page).toHaveAttribute('aria-busy', 'true')
  })

  it('should wait before showing loading', async () => {
    const { page } = await startSlowNavigation()

    await act(() => vi.advanceTimersByTimeAsync(BUSY_DELAY_MS - 1))

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(page).not.toHaveAttribute('aria-busy', 'true')
  })

  it('should drop loading once the page opens', async () => {
    const { page, finish } = await startSlowNavigation()
    await act(() => vi.advanceTimersByTimeAsync(BUSY_DELAY_MS))

    await act(async () => finish())

    expect(screen.getByRole('heading', { name: 'Slow page' })).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(page).not.toHaveAttribute('aria-busy', 'true')
  })

  it('should not warn about scroll positions when storage is blocked', async () => {
    const warn = vi.spyOn(console, 'warn')
    blockStorage('setItem')
    renderLayout(PAGE)
    await screen.findByText(SUBTITLE)

    window.dispatchEvent(new PageTransitionEvent('pagehide'))

    expect(warn).not.toHaveBeenCalled()
  })

  it('should skip loading when the page opens before the delay', async () => {
    const { finish } = await startSlowNavigation()
    await act(() => vi.advanceTimersByTimeAsync(BUSY_DELAY_MS - 1))
    await act(async () => finish())

    await act(() => vi.advanceTimersByTimeAsync(BUSY_DELAY_MS))

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})
