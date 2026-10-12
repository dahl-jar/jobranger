import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { routes } from '@/app/router'
import { renderRoutes } from '@/test/render'

describe('routes', () => {
  it('should show No page here for an unknown path', async () => {
    renderRoutes(routes, '/nowhere')

    expect(await screen.findByRole('heading', { name: 'No page here.' })).toBeInTheDocument()
  })

  it('should link Home to the landing page from an unknown path', async () => {
    renderRoutes(routes, '/nowhere')

    expect(await screen.findByRole('link', { name: 'Home' })).toHaveAttribute('href', '/')
  })
})
