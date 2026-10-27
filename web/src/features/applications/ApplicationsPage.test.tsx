import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { ApplicationsPage } from '@/features/applications/ApplicationsPage'
import { applicationsSnapshot, foundApplication, handApplication, pinTimeZone, TODAY } from '@/features/applications/testData'
import type { RemoveWithUndo } from '@/features/applications/useRemoveWithUndo'
import type { Application, ApplicationsSnapshot } from '@/features/relay/contract/payload.gen'
import { renderRoutes } from '@/test/render'

const bothListed: ApplicationsSnapshot = { ...applicationsSnapshot, applications: [foundApplication, handApplication] }

type PageRemoval = Pick<RemoveWithUndo, 'pending' | 'undo'>

interface PageOptions {
  snapshot?: ApplicationsSnapshot
  removal?: Partial<PageRemoval>
  editsDisabled?: boolean
  onCreate?: () => Promise<void>
}

function renderPage(path: string, options: PageOptions = {}) {
  const removal = { pending: undefined, undo: vi.fn(), ...options.removal }
  const onCreate = vi.fn(options.onCreate ?? (() => Promise.resolve()))
  const view = renderRoutes(
    [
      {
        path: '/app/applications',
        element: (
          <ApplicationsPage
            snapshot={options.snapshot ?? bothListed}
            today={TODAY}
            editsDisabled={options.editsDisabled ?? false}
            removal={removal}
            onCreate={onCreate}
            onStatusChange={vi.fn()}
          />
        ),
      },
      { path: '/app/applications/:applicationId', element: <p>Panel</p> },
    ],
    path,
  )
  return { ...view, removal, onCreate }
}

async function fillCompany(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Add by hand' }))
  await user.type(screen.getByLabelText('Company'), 'Org Three AS')
}

const companies = () =>
  screen
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell')[0]?.textContent)

describe('ApplicationsPage', () => {
  pinTimeZone()

  it('should leave removed applications out', () => {
    renderPage('/app/applications', { snapshot: applicationsSnapshot })

    expect(companies()).toEqual([foundApplication.company])
  })

  it('should count listed applications on the status pills', () => {
    renderPage('/app/applications', { snapshot: applicationsSnapshot })

    expect(screen.getAllByRole('button', { pressed: false }).map((pill) => pill.textContent)).toEqual([
      'Waiting 1',
      'Interview 0',
      'Offer 0',
      'Accepted 0',
      'Rejected 0',
    ])
  })

  it('should list only the picked status', async () => {
    const user = userEvent.setup()
    const { router } = renderPage('/app/applications')

    await user.click(screen.getByRole('button', { name: 'Interview 1' }))

    expect(companies()).toEqual([handApplication.company])
    expect(screen.getByRole('button', { name: 'Interview 1' })).toHaveAttribute('aria-pressed', 'true')
    expect(router.state.location.search).toBe('?status=interview')
  })

  it('should list every status again when the picked pill is pressed', async () => {
    const user = userEvent.setup()
    const { router } = renderPage('/app/applications?status=interview')

    await user.click(screen.getByRole('button', { name: 'Interview 1' }))

    expect(companies()).toEqual([foundApplication.company, handApplication.company])
    expect(router.state.location.search).toBe('')
  })

  it('should say so when no application has the picked status', () => {
    renderPage('/app/applications?status=offer')

    expect(screen.getByText('No applications here.')).toBeInTheDocument()
  })

  it('should leave out the empty message when applications are listed', () => {
    renderPage('/app/applications')

    expect(screen.queryByText('No applications here.')).not.toBeInTheDocument()
  })

  it('should sort by Applied oldest first when the Applied header is clicked', async () => {
    const user = userEvent.setup()
    const { router } = renderPage('/app/applications')

    await user.click(screen.getByRole('button', { name: 'Applied' }))

    expect(companies()).toEqual([handApplication.company, foundApplication.company])
    expect(router.state.location.search).toBe('?sort=applied.asc')
  })

  it('should sort rows by the sort in the address', () => {
    renderPage('/app/applications?sort=company.desc')

    expect(companies()).toEqual([handApplication.company, foundApplication.company])
  })

  it('should open an application keeping filter and sort', async () => {
    const user = userEvent.setup()
    const { router } = renderPage('/app/applications?status=waiting&sort=company.asc')

    await user.click(screen.getByRole('button', { name: foundApplication.company }))

    expect(router.state.location.pathname).toBe(`/app/applications/${foundApplication.id}`)
    expect(router.state.location.search).toBe('?status=waiting&sort=company.asc')
  })

  it('should hide the application waiting for removal', () => {
    renderPage('/app/applications', { removal: { pending: foundApplication } })

    expect(companies()).toEqual([handApplication.company])
  })

  it('should undo the removal from the Removed message', async () => {
    const user = userEvent.setup()
    const { removal } = renderPage('/app/applications', { removal: { pending: foundApplication } })
    const message = screen.getByRole('status')
    expect(message).toHaveTextContent(`Removed ${foundApplication.company}.`)

    await user.click(within(message).getByRole('button', { name: 'Undo' }))

    expect(removal.undo).toHaveBeenCalledOnce()
  })

  it.each([
    { total: 212, footer: 'Showing the latest 2 of 212. Click a row to open it.' },
    { total: 2, footer: 'Click a row to open it.' },
  ])('should say "$footer" when $total applications exist', ({ total, footer }) => {
    renderPage('/app/applications', { snapshot: { ...bothListed, applications_total: total } })

    expect(screen.getByText(/Click a row/).textContent).toBe(footer)
  })

  it('should list every status after adding', async () => {
    const user = userEvent.setup()
    const { router, onCreate } = renderPage('/app/applications?status=interview')
    await fillCompany(user)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(onCreate).toHaveBeenCalledExactlyOnceWith({ company: 'Org Three AS', title: '', note: '' })
    expect(router.state.location.search).toBe('')
  })

  it('should keep the status filter when the machine refuses the application', async () => {
    const user = userEvent.setup()
    const { router } = renderPage('/app/applications?status=interview', { onCreate: () => Promise.reject(new Error('no answer from your machine')) })
    await fillCompany(user)

    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Not saved')
    expect(router.state.location.search).toBe('?status=interview')
  })

  it('should close the form on Cancel', async () => {
    const user = userEvent.setup()
    renderPage('/app/applications')
    await user.click(screen.getByRole('button', { name: 'Add by hand' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByLabelText('Company')).not.toBeInTheDocument()
  })

  it('should disable the edit controls while edits are off', async () => {
    const user = userEvent.setup()
    renderPage('/app/applications', { editsDisabled: true })

    await user.click(screen.getByRole('button', { name: 'Add by hand' }))

    expect(screen.getByRole('button', { name: 'Status: Waiting' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Add' })).toBeDisabled()
  })

  it('should show the add form only after Add by hand', () => {
    renderPage('/app/applications')

    expect(screen.queryByLabelText('Company')).not.toBeInTheDocument()
  })

  it('should replace the address entry when the filter changes', async () => {
    const user = userEvent.setup()
    const { router } = renderPage('/app/applications')

    await user.click(screen.getByRole('button', { name: 'Interview 1' }))

    expect(router.state.historyAction).toBe('REPLACE')
  })

  it('should return focus to the restored row after Undo', async () => {
    const user = userEvent.setup()
    function WithPendingRemoval() {
      const [pending, setPending] = useState<Application | undefined>(foundApplication)
      return (
        <ApplicationsPage
          snapshot={bothListed}
          today={TODAY}
          editsDisabled={false}
          removal={{ pending, undo: () => setPending(undefined) }}
          onCreate={() => Promise.resolve()}
          onStatusChange={vi.fn()}
        />
      )
    }
    renderRoutes([{ path: '/app/applications', element: <WithPendingRemoval /> }], '/app/applications')

    await user.click(screen.getByRole('button', { name: 'Undo' }))

    expect(screen.getByRole('button', { name: foundApplication.company })).toHaveFocus()
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })
})
