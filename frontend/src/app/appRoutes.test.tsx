import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/router'
import { renderRoutes } from '@/test/render'
import { warmRoutes } from '@/test/warmRoutes'

describe('app routes', () => {
  warmRoutes(routes)

  it('should link Open the app from a missing page', async () => {
    renderRoutes(routes, '/nowhere')

    expect(await screen.findByRole('link', { name: 'Open the app' })).toHaveAttribute('href', '/app')
  })

  it('should link the demo from the landing header', async () => {
    renderRoutes(routes, '/')

    const header = await screen.findByRole('banner')

    expect(within(header).getByRole('link', { name: 'Demo' })).toHaveAttribute('href', '/demo')
  })

  it('should offer View demo beside Install on the landing page', async () => {
    renderRoutes(routes, '/')

    expect(await screen.findByRole('link', { name: 'View demo' })).toHaveAttribute('href', '/demo')
  })

  it('should show a missing page inside the app for an unknown path under it', async () => {
    renderRoutes(routes, '/demo/settings/nowhere/else')

    await screen.findByRole('heading', { name: 'No page here.' })

    expect(within(screen.getByRole('banner')).getByRole('link', { name: 'Settings' })).toBeInTheDocument()
  })

  it('should offer the demo on the pair page', async () => {
    renderRoutes(routes, '/pair')

    expect(await screen.findByRole('link', { name: 'View the demo' })).toHaveAttribute('href', '/demo')
  })
})
