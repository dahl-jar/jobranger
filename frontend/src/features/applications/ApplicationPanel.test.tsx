import { act, fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { Outlet, useOutletContext, useParams } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApplicationPanel } from '@/features/applications/ApplicationPanel'
import { ApplicationsPage } from '@/features/applications/ApplicationsPage'
import { NOTE_MAX_CHARS } from '@/features/applications/limits'
import { ApplicationsTable } from '@/features/applications/ApplicationsTable'
import { foundApplication, handApplication, pinTimeZone, TODAY } from '@/features/applications/testData'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { type RemoveWithUndo, useRemoveWithUndo } from '@/features/applications/useRemoveWithUndo'
import type { Application } from '@/features/relay/contract/payload.gen'
import { renderRoutes } from '@/test/render'

const SEARCH = '?sort=company.asc'
const roles = payloadFixtures['config.snapshot'].roles

let showModalCalls = 0

function stubDialog() {
  showModalCalls = 0
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value(this: HTMLDialogElement) {
      showModalCalls += 1
      this.setAttribute('open', '')
    },
  })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value(this: HTMLDialogElement) {
      this.removeAttribute('open')
    },
  })
}

function unstubDialog() {
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'close')
}

function renderPanel(application: Application = foundApplication, editsDisabled = false) {
  const handlers = { onStatusChange: vi.fn(), onNoteChange: vi.fn(), onRemove: vi.fn() }
  const view = renderRoutes(
    [
      {
        path: '/app/applications',
        element: (
          <>
            <ApplicationsTable
              applications={[foundApplication, handApplication]}
              sort={{ key: 'company', dir: 'asc' }}
              today={TODAY}
              editsDisabled={false}
              onSort={vi.fn()}
              onOpen={vi.fn()}
              onStatusChange={vi.fn()}
            />
            <Outlet />
          </>
        ),
        children: [
          {
            path: ':applicationId',
            element: <ApplicationPanel application={application} today={TODAY} roles={roles} editsDisabled={editsDisabled} {...handlers} />,
          },
        ],
      },
    ],
    `/app/applications/${application.id}${SEARCH}`,
  )
  return { ...view, ...handlers, dialog: screen.getByRole('dialog') }
}

const sendRemoval = vi.fn()

function PageWithPanel({ rows }: { rows: Application[] }) {
  const [notes, setNotes] = useState<Record<number, string>>({})
  const removal = useRemoveWithUndo(sendRemoval)
  const listed = rows.map((row) => ({ ...row, note: notes[row.id] ?? row.note }))
  return (
    <>
      <ApplicationsPage
        snapshot={{ applications: listed, applications_total: listed.length }}
        today={TODAY}
        editsDisabled={false}
        removal={removal}
        onCreate={() => Promise.resolve()}
        onStatusChange={vi.fn()}
      />
      <Outlet context={{ listed, removal, saveNote: (id: number, note: string) => setNotes((current) => ({ ...current, [id]: note })) }} />
    </>
  )
}

interface PanelContext {
  listed: Application[]
  removal: RemoveWithUndo
  saveNote: (id: number, note: string) => void
}

function PanelRoute() {
  const { listed, removal, saveNote } = useOutletContext<PanelContext>()
  const { applicationId } = useParams()
  const application = listed.find((row) => String(row.id) === applicationId)
  if (!application) return null
  return (
    <ApplicationPanel
      application={application}
      today={TODAY}
      roles={roles}
      editsDisabled={false}
      onStatusChange={vi.fn()}
      onNoteChange={saveNote}
      onRemove={removal.remove}
    />
  )
}

function renderPageRoutes(path: string, rows: Application[] = [foundApplication, handApplication]) {
  return renderRoutes(
    [{ path: '/app/applications', element: <PageWithPanel rows={rows} />, children: [{ path: ':applicationId', element: <PanelRoute /> }] }],
    path,
  )
}

function renderWithPage(application: Application, rows?: Application[]) {
  const view = renderPageRoutes(`/app/applications/${application.id}${SEARCH}`, rows)
  return { ...view, dialog: screen.getByRole('dialog') }
}

const pressEscape = (dialog: HTMLElement) => fireEvent(dialog, new Event('cancel', { cancelable: true }))

describe('ApplicationPanel', () => {
  pinTimeZone()

  beforeEach(stubDialog)
  afterEach(unstubDialog)

  it('should open as a modal dialog named by the role', () => {
    const { dialog } = renderPanel()

    expect(dialog).toHaveAttribute('open')
    expect(dialog).toHaveAccessibleName(foundApplication.title)
  })

  it('should return focus to the row on Escape', () => {
    const { dialog } = renderPanel()

    pressEscape(dialog)

    expect(screen.getByRole('button', { name: foundApplication.company })).toHaveFocus()
  })

  it.each([
    { name: 'Close', close: () => fireEvent.click(screen.getByRole('button', { name: 'Close' })) },
    { name: 'Done', close: () => fireEvent.click(screen.getByRole('button', { name: 'Done' })) },
  ])('should go back to the list keeping filter and sort on $name', ({ close }) => {
    const { router } = renderPanel()

    close()

    expect(router.state.location.pathname).toBe('/app/applications')
    expect(router.state.location.search).toBe(SEARCH)
  })

  it('should save a changed note on blur', async () => {
    const user = userEvent.setup()
    const { onNoteChange } = renderPanel()
    await user.type(screen.getByLabelText('Note'), 'Call back')

    await user.tab()

    expect(onNoteChange).toHaveBeenCalledExactlyOnceWith(foundApplication.id, 'Call back')
  })

  it('should not save an unchanged note on blur or Done', async () => {
    const user = userEvent.setup()
    const { onNoteChange } = renderPanel(handApplication)
    await user.click(screen.getByLabelText('Note'))
    await user.tab()

    await user.click(screen.getByRole('button', { name: 'Done' }))

    expect(onNoteChange).not.toHaveBeenCalled()
  })

  it('should save a note changed after blur once more on Done', async () => {
    const user = userEvent.setup()
    const { onNoteChange } = renderPanel()
    await user.type(screen.getByLabelText('Note'), 'Call')
    await user.tab()
    await user.type(screen.getByLabelText('Note'), ' back')

    await user.click(screen.getByRole('button', { name: 'Done' }))

    expect(onNoteChange.mock.calls).toEqual([
      [foundApplication.id, 'Call'],
      [foundApplication.id, 'Call back'],
    ])
  })

  it('should save a changed note when closed with Escape', async () => {
    const user = userEvent.setup()
    const { dialog, onNoteChange } = renderPanel()
    await user.type(screen.getByLabelText('Note'), 'Call back')

    pressEscape(dialog)

    expect(onNoteChange).toHaveBeenCalledExactlyOnceWith(foundApplication.id, 'Call back')
  })

  it('should change the status', async () => {
    const user = userEvent.setup()
    const { dialog, onStatusChange } = renderPanel()
    await user.click(within(dialog).getByRole('button', { name: 'Status: Waiting' }))

    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))

    expect(onStatusChange).toHaveBeenCalledExactlyOnceWith(foundApplication.id, 'offer')
  })

  it('should send the removal when Remove is pressed', async () => {
    const user = userEvent.setup()
    const { onRemove } = renderPanel()

    await user.click(screen.getByRole('button', { name: 'Remove from list' }))

    expect(onRemove).toHaveBeenCalledExactlyOnceWith(foundApplication)
  })

  it('should keep status, note and Remove from sending while edits are off', () => {
    const { dialog } = renderPanel(foundApplication, true)

    expect(within(dialog).getByRole('button', { name: 'Status: Waiting' })).toBeDisabled()
    expect(screen.getByLabelText('Note')).toHaveAttribute('readonly')
    expect(screen.getByRole('button', { name: 'Remove from list' })).toBeDisabled()
  })

  it('should list what the ad asks for', () => {
    renderPanel()

    expect(screen.getByRole('heading', { name: 'What the ad asks for' }).nextElementSibling).toHaveTextContent('React met')
  })

  it.each([
    { source: 'nav' as const, name: 'NAV' },
    { source: 'finn' as const, name: 'FINN' },
    { source: 'linkedin' as const, name: 'LinkedIn' },
    { source: 'teamtailor' as const, name: 'Teamtailor' },
    { source: 'ashby' as const, name: 'Ashby' },
  ])('should link to the ad on $name', ({ source, name }) => {
    renderPanel({ ...foundApplication, source })

    expect(screen.getByRole('link', { name: `Open the ad on ${name}` })).toHaveAttribute('href', foundApplication.url)
  })

  it('should link the company search with the name encoded', () => {
    renderPanel({ ...foundApplication, company: 'Org & Co' })

    expect(screen.getByRole('link', { name: 'Look up the company' })).toHaveAttribute(
      'href',
      'https://www.google.com/search?q=Org%20%26%20Co',
    )
  })

  it('should leave out the ad link for an application added by hand', () => {
    renderPanel({ ...handApplication, url: 'https://jobs.example.test/ad/1' })

    expect(screen.queryByRole('link', { name: /Open the ad/ })).not.toBeInTheDocument()
  })

  it.each([
    {
      name: 'found',
      application: { ...foundApplication, role_type: 'backend' as const },
      facts: ['Applied', 'Today', 'Deadline', '25 Oct', 'Fit', 'Partial fit', 'CV sent', 'cv.pdf'],
      meta: ['Place not set', 'Backend', 'NAV'],
    },
    {
      name: 'added by hand',
      application: handApplication,
      facts: ['Applied', '1 Oct', 'Deadline', 'Not set', 'Fit', 'Not checked', 'CV sent', 'Not recorded'],
      meta: ['Place not set', 'Added by hand'],
    },
  ])('should show the facts of an application $name', ({ application, facts, meta }) => {
    renderPanel(application)

    const terms = screen.getAllByRole('term').map((term) => term.textContent)
    const values = screen.getAllByRole('definition').map((value) => value.textContent)
    expect(terms.flatMap((term, index) => [term, values[index]])).toEqual(facts)
    expect([...(screen.getByRole('heading', { level: 2 }).nextElementSibling?.children ?? [])].map((part) => part.textContent)).toEqual(meta)
  })

  it('should save a changed note on Escape when its row is gone', async () => {
    const user = userEvent.setup()
    const { dialog, onNoteChange } = renderPanel({ ...foundApplication, id: 9 })
    await user.type(screen.getByLabelText('Note'), 'Call back')

    pressEscape(dialog)

    expect(onNoteChange).toHaveBeenCalledExactlyOnceWith(9, 'Call back')
  })

  it('should keep the browser from closing the dialog on Escape', () => {
    const { dialog } = renderPanel()

    expect(pressEscape(dialog)).toBe(false)
  })

  it('should name the dialog "Role not set" when the role is missing', () => {
    renderPanel(handApplication)

    expect(screen.getByRole('dialog')).toHaveAccessibleName('Role not set')
  })

  it('should leave out the ad link when the ad has no address', () => {
    renderPanel({ ...foundApplication, url: null })

    expect(screen.queryByText(/Open the ad/)).not.toBeInTheDocument()
  })

  it('should leave out the ad checks when there are none', () => {
    renderPanel(handApplication)

    expect(screen.queryByRole('heading', { name: 'What the ad asks for' })).not.toBeInTheDocument()
  })

  it('should list the history by day', () => {
    renderPanel(handApplication)

    expect(within(screen.getByRole('list', { name: 'History' })).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      '1 OctAdded by hand',
    ])
  })

  it('should save a changed note once when the panel closes through browser Back', async () => {
    const user = userEvent.setup()
    const { router, onNoteChange } = renderPanel()
    await user.type(screen.getByLabelText('Note'), 'Back button note')

    await act(() => router.navigate('/app/applications'))

    expect(onNoteChange).toHaveBeenCalledExactlyOnceWith(foundApplication.id, 'Back button note')
  })

  it('should return focus to the row when the panel closes through browser Back', async () => {
    const { router } = renderPanel()

    await act(() => router.navigate('/app/applications'))

    expect(screen.getByRole('button', { name: foundApplication.company })).toHaveFocus()
  })

  it('should open the dialog once when saving the note re-renders it', async () => {
    const user = userEvent.setup()
    const { dialog } = renderWithPage(foundApplication)
    await user.type(screen.getByLabelText('Note'), 'n')

    pressEscape(dialog)

    expect(showModalCalls).toBe(1)
    expect(await screen.findByRole('button', { name: foundApplication.company })).toHaveFocus()
  })

  it('should lock page scroll while open', async () => {
    const { dialog } = renderPanel()
    expect(document.documentElement.style.overflow).toBe('hidden')

    await act(async () => {
      pressEscape(dialog)
    })

    expect(document.documentElement.style.overflow).toBe('')
  })

  it('should move focus to the next row after Remove', async () => {
    const user = userEvent.setup()
    renderWithPage(foundApplication)

    await user.click(screen.getByRole('button', { name: 'Remove from list' }))

    expect(screen.queryByRole('button', { name: foundApplication.company })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: handApplication.company })).toHaveFocus()
  })

  it('should move focus to the previous row after removing the last row', async () => {
    const user = userEvent.setup()
    renderWithPage(handApplication)

    await user.click(screen.getByRole('button', { name: 'Remove from list' }))

    expect(screen.getByRole('button', { name: foundApplication.company })).toHaveFocus()
  })

  it('should move focus to Add by hand after removing the only row', async () => {
    const user = userEvent.setup()
    renderWithPage(foundApplication, [foundApplication])

    await user.click(screen.getByRole('button', { name: 'Remove from list' }))

    expect(screen.getByRole('button', { name: 'Add by hand' })).toHaveFocus()
  })

  it('should cap the note at the contract limit', () => {
    const { onNoteChange } = renderPanel()
    fireEvent.change(screen.getByLabelText('Note'), { target: { value: 'n'.repeat(NOTE_MAX_CHARS + 1) } })

    fireEvent.blur(screen.getByLabelText('Note'))

    expect(screen.getByLabelText('Note')).toHaveAttribute('maxlength', String(NOTE_MAX_CHARS))
    expect(onNoteChange).toHaveBeenCalledExactlyOnceWith(foundApplication.id, 'n'.repeat(NOTE_MAX_CHARS))
  })

  it('should describe the note by its limit when the note is at the limit', () => {
    renderPanel()

    fireEvent.change(screen.getByLabelText('Note'), { target: { value: 'n'.repeat(NOTE_MAX_CHARS) } })

    expect(screen.getByLabelText('Note')).toHaveAccessibleDescription(`Note stops at ${NOTE_MAX_CHARS} characters.`)
  })

  it('should stay open when a press inside the panel ends on the backdrop', () => {
    const { dialog, router } = renderPanel()
    fireEvent.pointerDown(screen.getByLabelText('Note'))

    fireEvent.click(dialog)

    expect(router.state.location.pathname).toBe(`/app/applications/${foundApplication.id}`)
  })

  it('should leave out the note limit message when the note is under the limit', () => {
    renderPanel()

    expect(screen.getByLabelText('Note')).not.toHaveAttribute('aria-describedby')
  })

  it('should close on a press that starts and ends on the backdrop', () => {
    const { dialog, router } = renderPanel()
    fireEvent.pointerDown(dialog)

    fireEvent.click(dialog)

    expect(router.state.location.pathname).toBe('/app/applications')
  })

  it('should stay open when a link inside is followed with the keyboard', async () => {
    const user = userEvent.setup()
    const { router } = renderPanel()
    screen.getByRole('link', { name: 'Look up the company' }).focus()

    await user.keyboard('{Enter}')

    expect(router.state.location.pathname).toBe(`/app/applications/${foundApplication.id}`)
  })

  it('should return focus to the second row when its panel closes', () => {
    const { dialog } = renderPanel(handApplication)

    pressEscape(dialog)

    expect(screen.getByRole('button', { name: handApplication.company })).toHaveFocus()
  })

  it('should replace the panel entry when a linked panel closes', () => {
    const { router } = renderPanel()

    fireEvent.click(screen.getByRole('button', { name: 'Done' }))

    expect(router.state.historyAction).toBe('REPLACE')
  })

  it('should go back to the list entry when a panel opened from the list closes', async () => {
    const user = userEvent.setup()
    const { router } = renderPageRoutes(`/app/applications${SEARCH}`)
    await user.click(screen.getByRole('button', { name: foundApplication.company }))

    await user.click(screen.getByRole('button', { name: 'Done' }))

    expect(router.state.historyAction).toBe('POP')
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(`/app/applications${SEARCH}`)
  })
})
