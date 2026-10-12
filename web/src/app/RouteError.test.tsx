import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { RouteError } from '@/app/RouteError'
import { renderRoutes } from '@/test/render'

const ERROR_TEXT = 'ads query exploded'

function Broken(): never {
  throw new Error(ERROR_TEXT)
}

function renderBroken() {
  return renderRoutes([{ path: '/', ErrorBoundary: RouteError, Component: Broken }], '/')
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
})
