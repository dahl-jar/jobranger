import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, onTestFinished, vi } from 'vitest'
import { stubLocation } from '@/test/stubs'

const SCROLL_POSITIONS_KEY = 'react-router-scroll-positions'
const PRELOAD_ERROR = 'vite:preloadError'
const FOREIGN_PATH = '//evil.example/x'

describe('main', () => {
  const preloadListeners: EventListenerOrEventListenerObject[] = []
  const addListener = window.addEventListener.bind(window)

  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('PerformanceNavigationTiming', class {})
    document.body.innerHTML = '<div id="root"></div>'
    vi.spyOn(window, 'addEventListener').mockImplementation((type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | AddEventListenerOptions) => {
      if (listener === null) return
      if (type === PRELOAD_ERROR) preloadListeners.push(listener)
      addListener(type, listener, options)
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    for (const listener of preloadListeners.splice(0)) window.removeEventListener(PRELOAD_ERROR, listener)
  })

  it('should show the landing page in the root element', async () => {
    await import('@/main')

    expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('should forget the saved position of the opened page on a new load', async () => {
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, JSON.stringify({ '/': 500 }))

    await import('@/main')

    expect(JSON.parse(sessionStorage.getItem(SCROLL_POSITIONS_KEY) ?? 'null')).toEqual({})
  })

  it('should name the missing root element when index.html has none', async () => {
    document.body.innerHTML = ''

    await expect(import('@/main')).rejects.toThrow('index.html is missing the #root element')
  })

  it('should load the current page again when a page chunk fails to preload', async () => {
    await import('@/main')
    await screen.findByRole('heading', { level: 1 })
    const assign = stubLocation()

    window.dispatchEvent(new Event(PRELOAD_ERROR, { cancelable: true }))

    expect(assign).toHaveBeenCalledWith(`${window.location.origin}/`)
  })

  it('should load the page being opened when its chunk fails to preload', async () => {
    const user = userEvent.setup()
    vi.doMock('@/features/setup/SetupPage', () => new Promise(() => undefined))
    onTestFinished(() => {
      vi.doUnmock('@/features/setup/SetupPage')
    })
    await import('@/main')
    await user.click(within(await screen.findByRole('contentinfo')).getByRole('link', { name: 'Setup walkthrough' }))
    const assign = stubLocation()

    window.dispatchEvent(new Event(PRELOAD_ERROR, { cancelable: true }))

    expect(assign).toHaveBeenCalledWith(`${window.location.origin}/setup`)
  })

  it('should keep the reload on this site when the path starts with two slashes', async () => {
    const { origin } = window.location
    window.history.replaceState(null, '', `${origin}${FOREIGN_PATH}`)
    onTestFinished(() => {
      window.history.replaceState(null, '', '/')
    })
    await import('@/main')
    await screen.findByRole('heading', { level: 1 })
    const assign = stubLocation()

    window.dispatchEvent(new Event(PRELOAD_ERROR, { cancelable: true }))

    expect(assign).toHaveBeenCalledWith(`${origin}${FOREIGN_PATH}`)
  })

  it('should load the page once when a chunk failure after a reload is reported twice', async () => {
    await import('@/main')
    await screen.findByRole('heading', { level: 1 })
    const assign = stubLocation()
    const afterReload = new TypeError('Importing a module script failed.')
    const report = (payload: Error) => window.dispatchEvent(Object.assign(new Event(PRELOAD_ERROR, { cancelable: true }), { payload }))
    report(new TypeError('Importing a module script failed.'))

    report(afterReload)
    report(afterReload)

    expect(assign).toHaveBeenCalledOnce()
  })
})
