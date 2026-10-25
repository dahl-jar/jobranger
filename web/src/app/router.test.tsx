import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import styles from '@/app/PublicLayout.module.css'
import { routes } from '@/app/router'
import { renderRoutes } from '@/test/render'

const LANDING_HEADLINE = 'jobranger checks Norwegian software and data engineering ads against your CV.'

describe('routes', () => {
  it('should show No page here for an unknown path', async () => {
    renderRoutes(routes, '/nowhere')

    expect(await screen.findByRole('heading', { name: 'No page here.' })).toBeInTheDocument()
  })

  it('should link Home to the landing page from an unknown path', async () => {
    renderRoutes(routes, '/nowhere')

    expect(await screen.findByRole('link', { name: 'Home' })).toHaveAttribute('href', '/')
  })

  it('should show the landing page at /', async () => {
    renderRoutes(routes, '/')

    expect(await screen.findByRole('heading', { level: 1, name: LANDING_HEADLINE })).toBeInTheDocument()
  })

  it('should show the landing nav in the header at /', async () => {
    renderRoutes(routes, '/')

    const header = await screen.findByRole('banner')

    expect(within(header).getByRole('link', { name: 'Questions' })).toHaveAttribute('href', '#faq')
    expect(within(header).getByRole('link', { name: 'Open the app' })).toHaveAttribute('href', '/app')
  })

  it('should show the setup walkthrough at /setup', async () => {
    renderRoutes(routes, '/setup?step=2')

    expect(await screen.findByRole('heading', { level: 1, name: 'Model and key' })).toBeInTheDocument()
  })

  it('should name the setup walkthrough in the header', async () => {
    renderRoutes(routes, '/setup')

    expect(await screen.findByText('Setup walkthrough')).toBeInTheDocument()
  })

  it('should link Home from the setup walkthrough', async () => {
    renderRoutes(routes, '/setup')

    expect(await screen.findByRole('link', { name: 'Home' })).toHaveAttribute('href', '/')
  })

  it('should give every landing nav link a section to land on', async () => {
    renderRoutes(routes, '/')

    const nav = await screen.findByRole('navigation', { name: 'Main' })

    for (const link of within(nav).getAllByRole('link')) expect(document.getElementById(link.getAttribute('href')?.slice(1) ?? '')).toBeInTheDocument()
  })

  it('should open another page at the top when a link is followed', async () => {
    const user = userEvent.setup()
    const scrollTo = vi.spyOn(window, 'scrollTo')
    renderRoutes(routes, '/')
    const footer = await screen.findByRole('contentinfo')
    scrollTo.mockClear()

    await user.click(within(footer).getByRole('link', { name: 'Setup walkthrough' }))

    await screen.findByRole('heading', { level: 1, name: 'Install' })
    expect(scrollTo).toHaveBeenCalledWith(0, 0)
  })

  it('should scroll to the section named in the URL after an earlier page load saved a position', async () => {
    sessionStorage.setItem('react-router-scroll-positions', JSON.stringify({ default: 500 }))
    const scrollTo = vi.spyOn(window, 'scrollTo')
    const scrollIntoView = vi.spyOn(Element.prototype, 'scrollIntoView')

    renderRoutes(routes, '/#faq')

    await screen.findByRole('heading', { level: 1 })
    expect(scrollIntoView.mock.contexts).toContain(document.getElementById('faq'))
    expect(scrollTo).not.toHaveBeenCalledWith(0, 500)
  })

  it.each([
    { path: '/', title: 'jobranger' },
    { path: '/setup?step=3', title: 'Setup, step 3 of 8 · jobranger' },
    { path: '/nowhere', title: 'No page here · jobranger' },
  ])('should title $path as "$title"', async ({ path, title }) => {
    renderRoutes(routes, path)

    await waitFor(() => expect(document.title).toBe(title))
  })

  it('should keep the header Install link apart from the setup link', async () => {
    renderRoutes(routes, '/')

    const install = await screen.findAllByRole('link', { name: 'Install' })

    expect(install.map((link) => link.getAttribute('href'))).toEqual(['#install'])
  })

  it('should scroll back to the old position when going back', async () => {
    const user = userEvent.setup()
    vi.spyOn(window, 'scrollY', 'get').mockReturnValue(1234)
    const scrollTo = vi.spyOn(window, 'scrollTo')
    const { router } = renderRoutes(routes, '/')
    await user.click(within(await screen.findByRole('contentinfo')).getByRole('link', { name: 'Setup walkthrough' }))
    await screen.findByRole('heading', { level: 1, name: 'Install' })

    await act(() => router.navigate(-1))

    await screen.findByRole('heading', { level: 1, name: LANDING_HEADLINE })
    expect(scrollTo).toHaveBeenLastCalledWith(0, 1234)
  })

  it('should open a visited page at the top when a link leads back to it', async () => {
    const user = userEvent.setup()
    vi.spyOn(window, 'scrollY', 'get').mockReturnValue(1234)
    const scrollTo = vi.spyOn(window, 'scrollTo')
    renderRoutes(routes, '/')
    await user.click(within(await screen.findByRole('contentinfo')).getByRole('link', { name: 'Setup walkthrough' }))
    await screen.findByRole('heading', { level: 1, name: 'Install' })

    await user.click(screen.getByRole('link', { name: 'Home' }))

    await screen.findByRole('heading', { level: 1, name: LANDING_HEADLINE })
    expect(scrollTo).toHaveBeenLastCalledWith(0, 0)
  })

  it('should keep the scroll position when Next changes the setup step', async () => {
    const user = userEvent.setup()
    const scrollTo = vi.spyOn(window, 'scrollTo')
    renderRoutes(routes, '/setup?step=2')
    const next = await screen.findByRole('button', { name: 'Next: Your CV and roles' })
    scrollTo.mockClear()

    await user.click(next)

    await screen.findByRole('heading', { level: 1, name: 'Your CV and roles' })
    expect(scrollTo).not.toHaveBeenCalled()
  })

  it('should keep the scroll position when a step list link changes the setup step', async () => {
    const user = userEvent.setup()
    const scrollTo = vi.spyOn(window, 'scrollTo')
    renderRoutes(routes, '/setup?step=2')
    const nav = await screen.findByRole('navigation', { name: 'Setup steps' })
    scrollTo.mockClear()

    await user.click(within(nav).getByRole('link', { name: /Dry run/ }))

    await screen.findByRole('heading', { level: 1, name: 'Dry run' })
    expect(scrollTo).not.toHaveBeenCalled()
  })

  it.each([
    { from: '/', link: 'Setup walkthrough', title: 'Install' },
    { from: '/setup', link: 'Home', title: LANDING_HEADLINE },
  ])('should focus the new page heading without scrolling after $link from $from', async ({ from, link, title }) => {
    const user = userEvent.setup()
    const focus = vi.spyOn(HTMLElement.prototype, 'focus')
    renderRoutes(routes, from)

    await user.click((await screen.findAllByRole('link', { name: link })).at(-1) ?? document.body)

    const heading = await screen.findByRole('heading', { level: 1, name: title })
    await waitFor(() => expect(heading).toHaveFocus())
    expect(focus.mock.calls[focus.mock.contexts.indexOf(heading)]).toEqual([{ preventScroll: true }])
  })

  it('should leave focus alone on the first page load', async () => {
    renderRoutes(routes, '/')

    await screen.findByRole('heading', { level: 1 })

    expect(document.body).toHaveFocus()
  })

  it.each(['/', '/nowhere'])('should keep the page heading at %s out of the tab order', async (path) => {
    renderRoutes(routes, path)

    expect(await screen.findByRole('heading', { level: 1 })).toHaveAttribute('tabindex', '-1')
  })

  it('should give the setup walkthrough the wide layout', async () => {
    renderRoutes(routes, '/setup')

    const page = (await screen.findByRole('banner')).parentElement

    expect([...(page?.classList ?? [])]).toContain(styles.wide)
  })
})
