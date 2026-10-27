import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/router'
import { stubDialogs } from '@/test/dialog'
import { renderRoutes } from '@/test/render'
import { warmRoutes } from '@/test/warmRoutes'

describe('DemoLayout', () => {
  warmRoutes(routes)

  stubDialogs()

  it('should open the demo on its today list', async () => {
    const { router } = renderRoutes(routes, '/demo')

    await waitFor(() => expect(router.state.location.pathname).toBe('/demo/today'))
  })

  it('should say the demo holds sample data that is not saved', async () => {
    renderRoutes(routes, '/demo/today')

    expect(await screen.findByRole('note')).toHaveTextContent('Demo · sample data, nothing is saved')
  })

  it('should show the sample matches', async () => {
    renderRoutes(routes, '/demo/today')

    expect(await screen.findByRole('link', { name: 'Data Engineer' })).toBeInTheDocument()
  })

  it('should keep list links inside the demo', async () => {
    renderRoutes(routes, '/demo/today')

    const lists = await screen.findByRole('navigation', { name: 'Lists' })

    expect(within(lists).getByRole('link', { name: /^Active/ })).toHaveAttribute('href', '/demo/active')
  })

  it('should keep Open the app inside the demo on a missing page', async () => {
    renderRoutes(routes, '/demo/no/such/page')

    expect(await screen.findByRole('link', { name: 'Open the app' })).toHaveAttribute('href', '/demo')
  })

  it('should show an application added by hand in the demo', async () => {
    const user = userEvent.setup()
    renderRoutes(routes, '/demo/applications')
    await user.click(await screen.findByRole('button', { name: 'Add by hand' }))

    await user.type(screen.getByRole('textbox', { name: 'Company' }), 'Org Four AS')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('button', { name: 'Org Four AS' })).toBeInTheDocument()
  })

  it('should keep a mark made in the demo', async () => {
    const user = userEvent.setup()
    renderRoutes(routes, '/demo/active')
    const row = (await screen.findByRole('link', { name: 'Platform Engineer' })).closest('article') ?? document.body

    await user.click(within(row).getByRole('button', { name: 'Skip' }))

    expect(within(row).getByText('Skipped')).toBeInTheDocument()
  })

  it('should date a demo mark by the demo day', async () => {
    const user = userEvent.setup()
    renderRoutes(routes, '/demo/active')
    const row = (await screen.findByRole('link', { name: 'Platform Engineer' })).closest('article') ?? document.body

    await user.click(within(row).getByRole('button', { name: 'I applied' }))

    expect(within(row).getByText('Applied today')).toBeInTheDocument()
  })
})
