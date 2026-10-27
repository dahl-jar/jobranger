import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { ProfileSettings } from '@/features/settings/ProfileSettings'

const PROFILE = payloadFixtures['profile.snapshot']

function renderProfile() {
  const onUpdate = vi.fn()
  render(
    <ProfileSettings profile={PROFILE} cvUpload={<p>CV slot</p>} onUpdate={onUpdate}>
      <p>Context slot</p>
    </ProfileSettings>,
  )
  return { onUpdate, user: userEvent.setup() }
}

describe('ProfileSettings', () => {
  it.each([
    { row: 'Experience', field: 'summary' },
    { row: 'Education', field: 'education' },
  ])('should send the changed $row row when it loses focus', async ({ row, field }) => {
    const { onUpdate, user } = renderProfile()
    await user.clear(screen.getByLabelText(row))
    await user.type(screen.getByLabelText(row), ' Three years of Go. ')

    await user.tab()

    expect(onUpdate).toHaveBeenCalledExactlyOnceWith({ [field]: 'Three years of Go.' })
  })

  it('should keep one Experience row after the machine saves it on an empty profile', async () => {
    const onUpdate = vi.fn()
    const user = userEvent.setup()
    const empty = payloadFixtures['profile.snapshot.empty']
    const { rerender } = render(<ProfileSettings profile={empty} cvUpload={null} onUpdate={onUpdate}>{null}</ProfileSettings>)
    await user.type(screen.getByLabelText('Experience'), 'Five years Java')
    await user.tab()

    rerender(<ProfileSettings profile={{ ...empty, summary: 'Five years Java' }} cvUpload={null} onUpdate={onUpdate}>{null}</ProfileSettings>)

    expect(screen.getAllByLabelText('Experience')).toHaveLength(1)
    expect(screen.getByLabelText('Education')).toHaveValue('')
  })

  it('should show a new summary from the machine', () => {
    const { rerender } = render(<ProfileSettings profile={PROFILE} cvUpload={null} onUpdate={vi.fn()}>{null}</ProfileSettings>)

    rerender(<ProfileSettings profile={{ ...PROFILE, summary: 'Three years of Go.' }} cvUpload={null} onUpdate={vi.fn()}>{null}</ProfileSettings>)

    expect(screen.getByLabelText('Experience')).toHaveValue('Three years of Go.')
  })

  it('should not send a row that did not change', async () => {
    const { onUpdate, user } = renderProfile()
    await user.click(screen.getByLabelText('Experience'))

    await user.tab()

    expect(onUpdate).not.toHaveBeenCalled()
  })

  it('should send the languages with a new one', async () => {
    const { onUpdate, user } = renderProfile()

    await user.type(screen.getByRole('textbox', { name: 'Languages' }), 'German{Enter}')

    expect(onUpdate).toHaveBeenCalledExactlyOnceWith({ languages: ['Norwegian', 'English', 'German'] })
  })

  it('should send the stack without a removed item', async () => {
    const { onUpdate, user } = renderProfile()

    await user.click(screen.getByRole('button', { name: 'Remove C#' }))

    expect(onUpdate).toHaveBeenCalledExactlyOnceWith({ stack: ['Python', 'TypeScript'] })
  })

  it('should keep project ids when a project is added', async () => {
    const { onUpdate, user } = renderProfile()

    await user.type(screen.getByRole('textbox', { name: 'Projects' }), 'Booking API{Enter}')

    expect(onUpdate).toHaveBeenCalledExactlyOnceWith({
      projects: [
        { id: 'p1', name: 'Job scraper' },
        { id: null, name: 'Booking API' },
      ],
    })
  })

  it.each([
    { row: 'Languages', maxLength: '60' },
    { row: 'Stack', maxLength: '60' },
    { row: 'Projects', maxLength: '120' },
  ])('should cap a $row item at $maxLength characters', ({ row, maxLength }) => {
    renderProfile()

    expect(screen.getByRole('textbox', { name: row })).toHaveAttribute('maxlength', maxLength)
  })

  it('should refuse a 21st project', async () => {
    const onUpdate = vi.fn()
    const projects = Array.from({ length: 20 }, (_, index) => ({ id: `p${index}`, name: `Project ${index}` }))
    render(<ProfileSettings profile={{ ...PROFILE, projects }} cvUpload={null} onUpdate={onUpdate}>{null}</ProfileSettings>)

    await userEvent.setup().type(screen.getByRole('textbox', { name: 'Projects' }), 'One more{Enter}')

    expect(onUpdate).not.toHaveBeenCalled()
    expect(screen.getByText('At most 20 projects.')).toBeInTheDocument()
  })

  it('should show the CV and context slots', () => {
    renderProfile()

    expect(screen.getByText('CV slot')).toBeInTheDocument()
    expect(screen.getByText('Context slot')).toBeInTheDocument()
  })
})
