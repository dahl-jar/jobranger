import { act, cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { RouteObject } from 'react-router'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import styles from '@/app/Notice.module.css'
import { RouteError } from '@/app/RouteError'
import { routeImport } from '@/app/routeImport'
import { renderRoutes } from '@/test/render'
import { blockStorage, IMPORT_FAILURE, stubLocation } from '@/test/stubs'

const ERROR_TEXT = 'ads query exploded'
const TARGET = '/setup?step=2'
const NOT_LOADED = 'Could not load this page.'
const failedImport = routeImport(() => Promise.reject(new TypeError(IMPORT_FAILURE)))
const loadedImport = routeImport(async () => ({ Component: () => <h1>Loaded</h1> }))

function Broken(): never {
  throw new Error(ERROR_TEXT)
}

function renderBroken() {
  return renderRoutes([{ path: '/', ErrorBoundary: RouteError, Component: Broken }], '/')
}

function renderSetup(lazy: NonNullable<RouteObject['lazy']>) {
  return renderRoutes([{ ErrorBoundary: RouteError, children: [{ path: '/setup', lazy }] }], TARGET)
}

describe('RouteError', () => {
  it('should show the error page without the error text when a route throws', async () => {
    renderBroken()

    expect(await screen.findByRole('heading', { name: 'Something broke on this page.' })).toBeInTheDocument()
    expect(screen.queryByText(ERROR_TEXT, { exact: false })).not.toBeInTheDocument()
  })

  it('should reload the page when Reload is pressed', async () => {
    const user = userEvent.setup()
    const reload = vi.fn()
    vi.stubGlobal('location', { reload })
    onTestFinished(() => {
      vi.unstubAllGlobals()
    })
    renderBroken()

    await user.click(await screen.findByRole('button', { name: 'Reload' }))

    expect(reload).toHaveBeenCalledOnce()
  })

  it('should show the error as a full page notice', async () => {
    renderBroken()

    expect([...(await screen.findByRole('main')).classList]).toEqual([styles.notice, styles.standalone])
  })

  it('should title the error page', async () => {
    renderBroken()

    await waitFor(() => expect(document.title).toBe('Something broke · jobranger'))
  })

  it('should focus the error heading', async () => {
    renderBroken()

    const heading = await screen.findByRole('heading', { level: 1 })

    await waitFor(() => expect(heading).toHaveFocus())
  })

  it('should link Home to the landing page', async () => {
    renderBroken()

    expect(await screen.findByRole('link', { name: 'Home' })).toHaveAttribute('href', '/')
  })

  it('should load the page again when its code fails to load', async () => {
    const assign = stubLocation()

    renderSetup(failedImport)

    await waitFor(() => expect(assign).toHaveBeenCalledWith(`${window.location.origin}${TARGET}`))
  })

  it('should ask to check the connection when page code fails to load', async () => {
    stubLocation()

    renderSetup(failedImport)

    expect(await screen.findByRole('heading', { level: 1, name: NOT_LOADED })).toBeInTheDocument()
    expect(screen.getByText('Check your connection and reload.')).toBeInTheDocument()
  })

  it('should title the error page "Could not load this page" when page code fails to load', async () => {
    stubLocation()

    renderSetup(failedImport)

    await waitFor(() => expect(document.title).toBe('Could not load this page · jobranger'))
  })

  it('should stay on the error page when the reload fails too', async () => {
    const assign = stubLocation()
    renderSetup(failedImport)
    await waitFor(() => expect(assign).toHaveBeenCalledOnce())
    cleanup()

    renderSetup(failedImport)

    await screen.findByRole('heading', { level: 1, name: NOT_LOADED })
    expect(assign).toHaveBeenCalledOnce()
  })

  it('should stay on the error page when storage is blocked', async () => {
    const assign = stubLocation()
    blockStorage('setItem')

    renderSetup(failedImport)

    await screen.findByRole('heading', { level: 1, name: NOT_LOADED })
    expect(assign).not.toHaveBeenCalled()
  })

  it('should open a loaded page when storage is blocked', async () => {
    blockStorage('removeItem')

    renderSetup(loadedImport)

    expect(await screen.findByRole('heading', { name: 'Loaded' })).toBeInTheDocument()
  })

  it('should load the page again on a later failure after a page loaded', async () => {
    const assign = stubLocation()
    renderSetup(failedImport)
    await waitFor(() => expect(assign).toHaveBeenCalledOnce())
    cleanup()
    renderSetup(loadedImport)
    await screen.findByRole('heading', { name: 'Loaded' })
    cleanup()

    renderSetup(failedImport)

    await waitFor(() => expect(assign).toHaveBeenCalledTimes(2))
  })

  it('should keep the error heading out of the tab order', async () => {
    renderBroken()

    expect(await screen.findByRole('heading', { level: 1 })).toHaveAttribute('tabindex', '-1')
  })

  it('should leave the page alone when its code throws', async () => {
    const assign = stubLocation()

    renderBroken()

    await screen.findByRole('heading', { level: 1, name: 'Something broke on this page.' })
    expect(assign).not.toHaveBeenCalled()
  })

  it('should load the next page again when its code also fails to load', async () => {
    const assign = stubLocation()
    const { router } = renderRoutes(
      [{ ErrorBoundary: RouteError, children: [{ path: '/setup', lazy: failedImport }, { path: '/pair', lazy: failedImport }] }],
      TARGET,
    )
    await waitFor(() => expect(assign).toHaveBeenCalledOnce())

    await act(() => router.navigate('/pair'))

    await waitFor(() => expect(assign).toHaveBeenLastCalledWith(`${window.location.origin}/pair`))
  })

  it('should load the page again on the next failure after the error page showed', async () => {
    const assign = stubLocation()
    renderSetup(failedImport)
    await waitFor(() => expect(assign).toHaveBeenCalledOnce())
    cleanup()
    renderSetup(failedImport)
    await screen.findByRole('heading', { level: 1, name: NOT_LOADED })
    cleanup()

    renderSetup(failedImport)

    await waitFor(() => expect(assign).toHaveBeenCalledTimes(2))
  })

  it('should stay on the error page when offline', async () => {
    const assign = stubLocation()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)

    renderSetup(failedImport)

    await screen.findByRole('heading', { level: 1, name: NOT_LOADED })
    expect(assign).not.toHaveBeenCalled()
  })

  it('should load the page again when it is opened again after the error page', async () => {
    const assign = stubLocation()
    const failure = new TypeError(IMPORT_FAILURE)
    const sameFailure = routeImport(() => Promise.reject(failure))
    const home = { path: '/', Component: () => <h1>Home page</h1> }
    renderSetup(failedImport)
    await waitFor(() => expect(assign).toHaveBeenCalledOnce())
    cleanup()
    const { router } = renderRoutes([{ ErrorBoundary: RouteError, children: [home, { path: '/setup', lazy: sameFailure }] }], TARGET)
    await screen.findByRole('heading', { level: 1, name: NOT_LOADED })
    await act(() => router.navigate('/'))

    await act(() => router.navigate(TARGET))

    await waitFor(() => expect(assign).toHaveBeenCalledTimes(2))
  })
})
