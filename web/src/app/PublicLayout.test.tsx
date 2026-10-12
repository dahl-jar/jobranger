import { screen } from '@testing-library/react'
import type { RouteObject } from 'react-router'
import { describe, expect, it } from 'vitest'
import { PublicLayout, type PublicLayoutHandle } from '@/app/PublicLayout'
import { renderRoutes } from '@/test/render'

const PATH = '/page'
const SUBTITLE = 'Setup walkthrough'
const PAGE: RouteObject = { path: PATH, handle: { subtitle: SUBTITLE } satisfies PublicLayoutHandle, Component: () => null }

function renderLayout(page: RouteObject) {
  return renderRoutes([{ Component: PublicLayout, children: [page] }], PATH)
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

  it('should leave out Home when showHome is not true', async () => {
    renderLayout({ ...PAGE, handle: { showHome: 'yes' } })

    await screen.findByRole('link', { name: 'jobranger' })
    expect(screen.queryByRole('link', { name: 'Home' })).not.toBeInTheDocument()
  })
})
