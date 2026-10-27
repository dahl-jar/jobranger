import { fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { ConfigPatch, ConfigSnapshot } from '@/features/relay/contract/payload.gen'
import { applyConfigPatch } from '@/features/settings/configPatch'
import { DeliverySettings } from '@/features/settings/DeliverySettings'
import { EVERY_TWO_HOURS } from '@/features/settings/settings.samples'

const CONFIG = payloadFixtures['config.snapshot']
const NO_EMAIL = payloadFixtures['config.snapshot.empty']
const EMAIL_ONLY = { ...CONFIG, notify: { ...CONFIG.notify, website: false } }
const KEEP_ONE = 'Keep at least one way to get matches.'
const LONGEST_ADDRESS = `${'a'.repeat(242)}@example.com`
const TOO_LONG_ADDRESS = `a${LONGEST_ADDRESS}`
const THREE_TIMES = { ...CONFIG, schedule: { ...CONFIG.schedule, times: ['03:00', '15:00', '20:00'] } }
const EMAIL_OFF = { ...CONFIG, notify: { ...CONFIG.notify, email: { ...CONFIG.notify.email, on: false } } }
const NAV_ONLY = { ...CONFIG, sources: { nav: true, employers: false, finn: false, linkedin: false } }

function renderDelivery(config: ConfigSnapshot = CONFIG) {
  const onPatch = vi.fn()
  render(<DeliverySettings config={config} onPatch={onPatch} />)
  return { onPatch, user: userEvent.setup() }
}

function OptimisticDelivery({ initial, onPatch }: { initial: ConfigSnapshot; onPatch: (patch: ConfigPatch) => void }) {
  const [config, setConfig] = useState<ConfigSnapshot>(initial)
  return (
    <DeliverySettings
      config={config}
      onPatch={(patch) => {
        onPatch(patch)
        setConfig((current) => applyConfigPatch(current, patch))
      }}
    />
  )
}

function renderOptimistic(initial: ConfigSnapshot = CONFIG) {
  const onPatch = vi.fn()
  render(<OptimisticDelivery initial={initial} onPatch={onPatch} />)
  return { onPatch, user: userEvent.setup() }
}

describe('DeliverySettings', () => {
  it('should send the email toggle clicked right after an address edit', async () => {
    const { onPatch, user } = renderOptimistic({ ...CONFIG, notify: { website: true, email: { on: false, address: 'user@example.com' } } })
    await user.clear(screen.getByLabelText('Address'))
    await user.type(screen.getByLabelText('Address'), 'other@example.com')

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch.mock.calls).toEqual([[{ notify: { email: { address: 'other@example.com' } } }], [{ notify: { email: { on: true } } }]])
  })

  it('should ask about the website clicked right after an address edit', async () => {
    const { user } = renderOptimistic()
    await user.clear(screen.getByLabelText('Address'))
    await user.type(screen.getByLabelText('Address'), 'other@example.com')

    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    expect(screen.getByText('This page stops getting updates. Turn off?')).toBeInTheDocument()
  })

  it.each([
    ['Add a run time', ['08:00', '15:00', '17:00']],
    ['Remove run time 2', ['08:00']],
  ])('should send the edited run times with %s clicked right after an edit', async (button, times) => {
    const { onPatch, user } = renderOptimistic()
    await user.click(screen.getByLabelText('Run time 1'))
    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '08:00' } })

    await user.click(screen.getByRole('button', { name: button }))

    expect(onPatch).toHaveBeenLastCalledWith({ schedule: { times } })
  })

  it('should title each part of delivery', () => {
    renderDelivery()

    expect(screen.getAllByRole('heading').map((heading) => heading.textContent)).toEqual(['Website', 'Daily email', 'Sources', 'Run times'])
  })

  it('should refuse to turn off email when the website is off', async () => {
    const { onPatch, user } = renderDelivery(EMAIL_ONLY)

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent(KEEP_ONE)
  })

  it('should refuse to turn off the website when email is off', async () => {
    const { onPatch, user } = renderDelivery(NO_EMAIL)

    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent(KEEP_ONE)
  })

  it('should turn off the website after Turn off is confirmed', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))
    expect(screen.getByText('This page stops getting updates. Turn off?')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Turn off' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { website: false } })
    expect(screen.queryByText('This page stops getting updates. Turn off?')).not.toBeInTheDocument()
  })

  it('should move focus into the website question', async () => {
    const { user } = renderDelivery()

    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    expect(screen.getByRole('group', { name: 'This page stops getting updates. Turn off?' })).toContainElement(document.activeElement as HTMLElement)
  })

  it.each(['Turn off', 'Cancel'])('should return focus to the website toggle after %s', async (button) => {
    const { user } = renderDelivery()
    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    await user.click(screen.getByRole('button', { name: button }))

    expect(screen.getByRole('checkbox', { name: 'Show matches on this website' })).toHaveFocus()
  })

  it('should not ask about the website before it is unticked', () => {
    renderDelivery()

    expect(screen.queryByText('This page stops getting updates. Turn off?')).not.toBeInTheDocument()
  })

  it('should drop the delivery warning after a later change is sent', async () => {
    const { user } = renderDelivery(EMAIL_ONLY)
    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should keep the website on when Cancel is picked', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Turn off' })).not.toBeInTheDocument()
  })

  it('should turn the website back on without asking', async () => {
    const { onPatch, user } = renderDelivery(EMAIL_ONLY)

    await user.click(screen.getByRole('checkbox', { name: 'Show matches on this website' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { website: true } })
  })

  it('should turn email off when the website stays on', async () => {
    const { onPatch, user } = renderDelivery()

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { email: { on: false } } })
  })

  it('should turn email on with the saved address', async () => {
    const { onPatch, user } = renderDelivery(EMAIL_OFF)

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { email: { on: true } } })
  })

  it('should ask for an address before turning email on', async () => {
    const { onPatch, user } = renderDelivery(NO_EMAIL)

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Enter an address first.')
  })

  it('should turn email on with a typed address that is not saved yet', async () => {
    const { onPatch, user } = renderDelivery(NO_EMAIL)
    fireEvent.change(screen.getByLabelText('Address'), { target: { value: ' user@example.com ' } })

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { email: { on: true, address: 'user@example.com' } } })
  })

  it('should refuse to turn email on with a bad typed address', async () => {
    const { onPatch, user } = renderDelivery(NO_EMAIL)
    fireEvent.change(screen.getByLabelText('Address'), { target: { value: 'user@localhost' } })

    await user.click(screen.getByRole('checkbox', { name: 'Send me the daily list by email' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Check the address.')
  })

  it('should send a changed address when the field loses focus', async () => {
    const { onPatch, user } = renderDelivery()
    await user.clear(screen.getByLabelText('Address'))
    await user.type(screen.getByLabelText('Address'), ' other@example.com ')

    await user.tab()

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { email: { address: 'other@example.com' } } })
  })

  it('should send an address of the longest allowed length', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByLabelText('Address'))
    fireEvent.change(screen.getByLabelText('Address'), { target: { value: LONGEST_ADDRESS } })

    await user.tab()

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { email: { address: LONGEST_ADDRESS } } })
  })

  it('should clear the address when email is off', async () => {
    const { onPatch, user } = renderDelivery(EMAIL_OFF)

    await user.clear(screen.getByLabelText('Address'))
    await user.tab()

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ notify: { email: { address: null } } })
  })

  it('should keep the address while email is on', async () => {
    const { onPatch, user } = renderDelivery()

    await user.clear(screen.getByLabelText('Address'))
    await user.tab()

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Turn the email off before clearing the address.')
  })

  it('should not send an address that did not change', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByLabelText('Address'))

    await user.tab()

    expect(onPatch).not.toHaveBeenCalled()
  })

  it.each(['user@localhost', TOO_LONG_ADDRESS])('should refuse "%s" as an address', async (typed) => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByLabelText('Address'))
    fireEvent.change(screen.getByLabelText('Address'), { target: { value: typed } })

    await user.tab()

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Check the address.')
    expect(screen.getByLabelText('Address')).toHaveAttribute('aria-invalid', 'true')
  })

  it('should not mark a good address as invalid', () => {
    renderDelivery()

    expect(screen.getByLabelText('Address')).toHaveAttribute('aria-invalid', 'false')
  })

  it.each([
    { source: 'Employer feeds', hint: 'Teamtailor and Ashby' },
    { source: 'FINN', hint: 'Off by default. For personal use only.' },
    { source: 'LinkedIn', hint: 'Off by default. For personal use only.' },
  ])('should describe $source as $hint', ({ source, hint }) => {
    renderDelivery()

    expect(screen.getByRole('checkbox', { name: source })).toHaveAccessibleDescription(hint)
  })

  it('should leave NAV without a description', () => {
    renderDelivery()

    expect(screen.getByRole('checkbox', { name: 'NAV' })).not.toHaveAttribute('aria-describedby')
  })

  it.each([
    { name: 'FINN', patch: { finn: true } },
    { name: 'NAV', patch: { nav: false } },
  ])('should send the $name source toggle', async ({ name, patch }) => {
    const { onPatch, user } = renderDelivery()

    await user.click(screen.getByRole('checkbox', { name }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ sources: patch })
  })

  it('should keep at least one source on', async () => {
    const { onPatch, user } = renderDelivery(NAV_ONLY)

    await user.click(screen.getByRole('checkbox', { name: 'NAV' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Keep at least one source on.')
  })

  it('should drop the source warning after a later toggle', async () => {
    const { user } = renderDelivery(NAV_ONLY)
    await user.click(screen.getByRole('checkbox', { name: 'NAV' }))

    await user.click(screen.getByRole('checkbox', { name: 'FINN' }))

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should show new run times from the machine', () => {
    const { rerender } = render(<DeliverySettings config={CONFIG} onPatch={vi.fn()} />)

    rerender(<DeliverySettings config={{ ...CONFIG, schedule: { ...CONFIG.schedule, times: ['06:00'] } }} onPatch={vi.fn()} />)

    expect(screen.getByLabelText('Run time 1')).toHaveValue('06:00')
    expect(screen.queryByLabelText('Run time 2')).not.toBeInTheDocument()
  })

  it('should add the next free run time', async () => {
    const { onPatch, user } = renderDelivery()

    await user.click(screen.getByRole('button', { name: 'Add a run time' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ schedule: { times: ['03:00', '15:00', '17:00'] } })
  })

  it('should disable Add a run time when the day is full', () => {
    renderDelivery({ ...CONFIG, schedule: { ...CONFIG.schedule, times: EVERY_TWO_HOURS } })

    expect(screen.getByRole('button', { name: 'Add a run time' })).toBeDisabled()
  })

  it('should disable Add a run time while the run times have an error', () => {
    renderDelivery()

    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '14:00' } })

    expect(screen.getByRole('button', { name: 'Add a run time' })).toBeDisabled()
  })

  it('should not send run times that stay too close after a removal', async () => {
    const { onPatch, user } = renderDelivery(THREE_TIMES)
    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '14:00' } })

    await user.click(screen.getByRole('button', { name: 'Remove run time 3' }))

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })

  it('should ask for a full time when a run time is cleared', () => {
    renderDelivery()

    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '' } })

    expect(screen.getByRole('alert')).toHaveTextContent('Use a time like 03:00.')
  })

  it('should remove a run time', async () => {
    const { onPatch, user } = renderDelivery()

    await user.click(screen.getByRole('button', { name: 'Remove run time 1' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ schedule: { times: ['15:00'] } })
  })

  it('should send changed run times in order when the field loses focus', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByLabelText('Run time 1'))
    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '20:00' } })

    await user.tab()

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ schedule: { times: ['15:00', '20:00'] } })
  })

  it('should not send run times that are too close', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByLabelText('Run time 1'))
    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '14:00' } })

    await user.tab()

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('14:00 and 15:00 are less than 2 hours apart.')
  })

  it('should say when a run time is listed twice', () => {
    renderDelivery()

    fireEvent.change(screen.getByLabelText('Run time 1'), { target: { value: '15:00' } })

    expect(screen.getByRole('alert')).toHaveTextContent('15:00 is in the list twice.')
  })

  it('should not send unchanged run times when the field loses focus', async () => {
    const { onPatch, user } = renderDelivery()
    await user.click(screen.getByLabelText('Run time 1'))

    await user.tab()

    expect(onPatch).not.toHaveBeenCalled()
  })
})
