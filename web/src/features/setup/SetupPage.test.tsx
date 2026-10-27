import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SetupPage } from '@/features/setup/SetupPage'
import { renderRoutes } from '@/test/render'

const SEVEN_ROLES = ['Backend', 'Frontend', 'Fullstack', 'Mobile', 'Data engineering', 'DevOps and platform', 'ML and AI']

function renderSetup(search = '') {
  return renderRoutes([{ path: '/setup', Component: SetupPage }], `/setup${search}`)
}

function stepLinks() {
  return within(screen.getByRole('navigation', { name: 'Setup steps' })).getAllByRole('link')
}

describe('SetupPage', () => {
  it('should show the step from the URL', async () => {
    renderSetup('?step=3')

    expect(await screen.findByRole('heading', { level: 1, name: 'Your CV and roles' })).toBeInTheDocument()
    expect(screen.getByText('Step 3 of 8')).toBeInTheDocument()
  })

  it.each([
    { search: '?step=0', title: 'Install' },
    { search: '?step=9', title: 'Run every night' },
    { search: '?step=abc', title: 'Install' },
  ])('should show $title when the URL asks for $search', async ({ search, title }) => {
    renderSetup(search)

    expect(await screen.findByRole('heading', { level: 1, name: title })).toBeInTheDocument()
  })

  it('should move to the next step on Next', async () => {
    const user = userEvent.setup()
    renderSetup('?step=2')

    await user.click(await screen.findByRole('button', { name: 'Next: Your CV and roles' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Your CV and roles' })).toBeInTheDocument()
  })

  it('should move to the previous step on Back', async () => {
    const user = userEvent.setup()
    renderSetup('?step=2')

    await user.click(await screen.findByRole('button', { name: 'Back' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Install' })).toBeInTheDocument()
  })

  it('should disable Back on the first step', async () => {
    renderSetup()

    expect(await screen.findByRole('button', { name: 'Back' })).toBeDisabled()
  })

  it('should mark only the current step in the step list', async () => {
    renderSetup('?step=3')
    await screen.findByRole('heading', { level: 1, name: 'Your CV and roles' })

    expect(stepLinks().map((link) => link.getAttribute('aria-current'))).toEqual([null, null, 'step', null, null, null, null, null])
  })

  it('should mark the steps before the current one as done', async () => {
    renderSetup('?step=3')
    await screen.findByRole('heading', { level: 1, name: 'Your CV and roles' })

    expect(stepLinks().map((link) => link.textContent?.endsWith('done'))).toEqual([true, true, false, false, false, false, false, false])
  })

  it('should link each step in the step list to its URL', async () => {
    renderSetup()
    await screen.findByRole('heading', { level: 1, name: 'Install' })

    expect(stepLinks().map((link) => link.getAttribute('href'))).toEqual(['1', '2', '3', '4', '5', '6', '7', '8'].map((step) => `/setup?step=${step}`))
  })

  it('should offer Connect the website in place of Next on the last step', async () => {
    renderSetup('?step=8')

    expect(await screen.findByRole('link', { name: 'Connect the website' })).toHaveAttribute('href', '/pair')
    expect(screen.queryByRole('button', { name: /^Next/ })).not.toBeInTheDocument()
  })

  it("should show this site's pair URL in the pair step", async () => {
    renderSetup('?step=7')

    expect(await screen.findByRole('log', { name: 'Terminal output' })).toHaveTextContent(`${window.location.origin}/pair`)
    expect(screen.getByRole('link', { name: 'enter the code' })).toHaveAttribute('href', '/pair')
  })

  it('should leave out the pair link on other steps', async () => {
    renderSetup('?step=6')
    await screen.findByRole('heading', { level: 1, name: 'Dry run' })

    expect(screen.queryByRole('link', { name: 'enter the code' })).not.toBeInTheDocument()
  })

  it('should keep the terminal output silent for screen readers', async () => {
    renderSetup('?step=2')

    expect(await screen.findByRole('log', { name: 'Terminal output' })).toHaveAttribute('aria-live', 'off')
  })

  it('should list the seven roles in the roles step', async () => {
    renderSetup('?step=3')

    const terminal = await screen.findByRole('log', { name: 'Terminal output' })

    for (const role of SEVEN_ROLES) expect(terminal).toHaveTextContent(role)
  })

  it('should list the eight steps in order', async () => {
    renderSetup()
    await screen.findByRole('heading', { level: 1, name: 'Install' })

    expect(stepLinks().map((link) => link.textContent?.replace(/^\d+|, done$/g, ''))).toEqual([
      'Install',
      'Model and key',
      'Your CV and roles',
      'Limits and sources',
      'Delivery',
      'Dry run',
      'Pair the website',
      'Run every night',
    ])
  })

  it.each([
    { search: '?step=1', line: '$ pip install jobranger' },
    { search: '?step=2', line: '? Which AI provider should read the ads?' },
  ])('should show the terminal prompt in "$line"', async ({ search, line }) => {
    renderSetup(search)

    expect(await screen.findByRole('log', { name: 'Terminal output' })).toHaveTextContent(line)
  })

  it('should leave focus alone when the page opens', async () => {
    renderSetup('?step=3')

    await screen.findByRole('heading', { level: 1, name: 'Your CV and roles' })

    expect(document.body).toHaveFocus()
  })

  it('should retitle the page after Next', async () => {
    const user = userEvent.setup()
    renderSetup('?step=2')

    await user.click(await screen.findByRole('button', { name: 'Next: Your CV and roles' }))

    await waitFor(() => expect(document.title).toBe('Setup, step 3 of 8 · jobranger'))
  })

  it('should keep the step heading out of the tab order', async () => {
    renderSetup()

    expect(await screen.findByRole('heading', { level: 1, name: 'Install' })).toHaveAttribute('tabindex', '-1')
  })

  it('should focus the new step heading with the least scrolling', async () => {
    const user = userEvent.setup()
    const focus = vi.spyOn(HTMLElement.prototype, 'focus')
    const scrollIntoView = vi.spyOn(Element.prototype, 'scrollIntoView')
    renderSetup('?step=2')

    await user.click(await screen.findByRole('button', { name: 'Next: Your CV and roles' }))

    const heading = screen.getByRole('heading', { level: 1, name: 'Your CV and roles' })
    expect(heading).toHaveFocus()
    expect(focus.mock.calls[focus.mock.contexts.indexOf(heading)]).toEqual([{ preventScroll: true }])
    expect(scrollIntoView.mock.calls[scrollIntoView.mock.contexts.indexOf(heading)]).toEqual([{ block: 'nearest' }])
  })
})
