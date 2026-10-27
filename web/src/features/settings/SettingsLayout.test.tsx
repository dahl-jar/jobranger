import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SettingsLayout } from '@/features/settings/SettingsLayout'
import type { SaveResult } from '@/features/settings/SavedLine'
import { renderRoutes } from '@/test/render'

function renderSettings(path: string, { editsDisabled = false, saveResult }: { editsDisabled?: boolean; saveResult?: SaveResult } = {}) {
  return renderRoutes(
    [
      {
        path: '/app/settings/:section',
        element: (
          <SettingsLayout
            editsDisabled={editsDisabled}
            saveResult={saveResult}
            renderSection={(section) => <button type="button">Edit {section}</button>}
          />
        ),
      },
      { path: '/app/today', element: <p>Jobs</p> },
    ],
    path,
  )
}

describe('SettingsLayout', () => {
  it('should list the settings sections with Matching after Model', () => {
    renderSettings('/app/settings/search')

    const nav = screen.getByRole('navigation', { name: 'Settings' })
    const sectionLinks = Array.from(nav.querySelectorAll('a[href^="/app/settings/"]'), (link) => link.textContent)
    expect(sectionLinks).toEqual(['Search', 'Delivery', 'Appearance', 'Model', 'Matching', 'Connection', 'Profile'])
  })

  it('should redirect an unknown section to search', async () => {
    const { router } = renderSettings('/app/settings/learning')

    expect(await screen.findByRole('heading', { name: 'Search' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/app/settings/search')
  })

  it('should show the open section with its title and note', () => {
    renderSettings('/app/settings/matching')

    expect(screen.getByRole('heading', { name: 'Matching' })).toBeInTheDocument()
    expect(screen.getByText('Applies from the next run.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit matching' })).toBeInTheDocument()
  })

  it('should mark the open section as the current page', () => {
    renderSettings('/app/settings/model')

    expect(screen.getByRole('link', { name: 'Model' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Search' })).not.toHaveAttribute('aria-current')
  })

  it('should link back to the jobs', () => {
    renderSettings('/app/settings/search')

    expect(screen.getByRole('link', { name: 'Back to jobs' })).toHaveAttribute('href', '/app/today')
  })

  it('should lock profile while the machine is away', () => {
    renderSettings('/app/settings/profile', { editsDisabled: true })

    expect(screen.getByRole('button', { name: 'Edit profile' })).toBeDisabled()
  })

  it('should keep matching editable while the machine is away', () => {
    renderSettings('/app/settings/matching', { editsDisabled: true })

    expect(screen.getByRole('button', { name: 'Edit matching' })).toBeEnabled()
  })

  it('should keep edits on while the machine is connected', () => {
    renderSettings('/app/settings/profile')

    expect(screen.getByRole('button', { name: 'Edit profile' })).toBeEnabled()
  })

  it('should show the save result', () => {
    renderSettings('/app/settings/search', { saveResult: { kind: 'saved', recheck: undefined } })

    expect(screen.getByRole('status')).toHaveTextContent('Saved')
  })
})
