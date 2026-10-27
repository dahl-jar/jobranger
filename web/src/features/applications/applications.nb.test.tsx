import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { AddApplicationForm } from '@/features/applications/AddApplicationForm'
import { ApplicationPanel } from '@/features/applications/ApplicationPanel'
import { ApplicationsPage } from '@/features/applications/ApplicationsPage'
import { ApplicationsSummary } from '@/features/applications/ApplicationsSummary'
import { StatusMenu } from '@/features/applications/StatusMenu'
import { applicationsSnapshot, foundApplication, handApplication, pinTimeZone, TODAY } from '@/features/applications/testData'
import { CommandError } from '@/features/relay/commands'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian } from '@/test/render'

const ROLES = payloadFixtures['config.snapshot'].roles
const APPLICATION_DATA = [
  ...[foundApplication, handApplication].flatMap((application) => [
    application.company,
    application.title,
    application.note,
    application.cv_name ?? '',
    ...application.checks.map((check) => check.text),
    ...application.history.map((entry) => entry.text),
  ]),
  ...ROLES.map((role) => role.label),
]

pinTimeZone()

function stubDialog() {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value(this: HTMLDialogElement) {
      this.setAttribute('open', '')
    },
  })
  onTestFinished(() => {
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal')
  })
}

describe('applications in bokmål', () => {
  it('should show the list without English text', async () => {
    const user = userEvent.setup()
    renderNorwegian(
      <ApplicationsPage
        snapshot={{ applications: [foundApplication, handApplication], applications_total: applicationsSnapshot.applications_total + 5 }}
        today={TODAY}
        editsDisabled={false}
        removal={{ pending: handApplication, undo: vi.fn() }}
        onCreate={vi.fn()}
        onStatusChange={vi.fn()}
      />,
    )

    await user.click(await screen.findByRole('button', { name: 'Legg til selv' }))

    expect(screen.getByRole('form', { name: 'Legg til en søknad' })).toBeInTheDocument()
    expectNoEnglish(APPLICATION_DATA)
  })

  it('should hint at a note only in the note field', async () => {
    renderNorwegian(<AddApplicationForm onAdd={vi.fn()} onCancel={vi.fn()} disabled={false} />)

    expect(await screen.findByLabelText('Notat')).toHaveAttribute('placeholder', 'f.eks. Søkte via en anbefaling')
    expect(screen.getByLabelText('Selskap')).not.toHaveAttribute('placeholder')
  })

  it('should say the machine did not answer in bokmål', async () => {
    const user = userEvent.setup()
    const onAdd = vi.fn(() => Promise.reject(new CommandError('timeout', 'no answer from your machine.')))
    renderNorwegian(<AddApplicationForm onAdd={onAdd} onCancel={vi.fn()} disabled={false} />)
    await user.type(await screen.findByLabelText('Selskap'), 'Org Three AS')

    await user.click(screen.getByRole('button', { name: 'Legg til' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Ikke lagret: ingen svar fra maskinen din.')
  })

  it('should ask for a company in bokmål', async () => {
    const user = userEvent.setup()
    renderNorwegian(<AddApplicationForm onAdd={vi.fn()} onCancel={vi.fn()} disabled={false} />)

    await user.click(await screen.findByRole('button', { name: 'Legg til' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Skriv inn et selskap.')
  })

  it.each([
    { name: 'a found application', application: foundApplication },
    { name: 'an application added by hand', application: handApplication },
  ])('should show $name in the panel without English text', async ({ application }) => {
    stubDialog()
    renderNorwegian(
      <ApplicationPanel
        application={application}
        today={TODAY}
        roles={ROLES}
        editsDisabled={false}
        onStatusChange={vi.fn()}
        onNoteChange={vi.fn()}
        onRemove={vi.fn()}
      />,
    )

    expect(await screen.findByRole('button', { name: 'Fjern fra listen' })).toBeInTheDocument()
    expectNoEnglish(APPLICATION_DATA)
  })

  it.each([
    { name: 'applications', applications: [foundApplication, handApplication] },
    { name: 'no applications', applications: [] },
  ])('should sum up $name without English text', async ({ applications }) => {
    renderNorwegian(<ApplicationsSummary applications={applications} today={TODAY} />)

    expect(await screen.findByRole('heading', { name: 'Søknader' })).toBeInTheDocument()
    expectNoEnglish(APPLICATION_DATA)
  })

  it('should say an application went out today', async () => {
    renderNorwegian(<ApplicationsSummary applications={[foundApplication]} today={TODAY} />)

    expect(await screen.findByText('sendt i dag')).toBeInTheDocument()
  })

  it('should name the status menu items in bokmål', async () => {
    const user = userEvent.setup()
    renderNorwegian(<StatusMenu status="offer" disabled={false} onChange={vi.fn()} />)

    await user.click(await screen.findByRole('button', { name: 'Status: Tilbud' }))

    expect(screen.getByRole('menu', { name: 'Søknadsstatus' })).toBeInTheDocument()
    expectNoEnglish()
  })
})
