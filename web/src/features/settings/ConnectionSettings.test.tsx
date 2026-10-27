import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Status } from '@/features/relay/contract/payload.gen'
import { ConnectionSettings } from '@/features/settings/ConnectionSettings'
import { TODAY } from '@/features/settings/settings.samples'
import { formatClock } from '@/shared/format/dates'

const STATUS = payloadFixtures.status
const PERSIST_WARNING = 'This browser may clear the pairing after 7 days without a visit.'

function renderConnection({ status = STATUS, isOnline = true, isPersisted = true }: { status?: Status; isOnline?: boolean; isPersisted?: boolean } = {}) {
  const onUnpair = vi.fn()
  render(<ConnectionSettings status={status} isOnline={isOnline} isPersisted={isPersisted} today={TODAY} onUnpair={onUnpair} />)
  return { onUnpair, user: userEvent.setup() }
}

describe('ConnectionSettings', () => {
  it('should show the machine facts', () => {
    renderConnection()

    expect(screen.getByText(STATUS.machine)).toBeInTheDocument()
    expect(screen.getByText(`jobranger 0.1.0 · paired 9 Oct · last run ${formatClock('2026-10-12T03:04:00Z')}`)).toBeInTheDocument()
  })

  it('should leave out the last run before the first run', () => {
    renderConnection({ status: payloadFixtures['status.idle'] })

    expect(screen.getByText('jobranger 0.1.0 · paired 9 Oct')).toBeInTheDocument()
  })

  it.each([
    { isOnline: true, word: 'Online' },
    { isOnline: false, word: 'Offline' },
  ])('should say $word', ({ isOnline, word }) => {
    renderConnection({ isOnline })

    expect(screen.getByText(word)).toBeInTheDocument()
  })

  it('should unpair after the browser asks to confirm', async () => {
    const { onUnpair, user } = renderConnection()
    await user.click(screen.getByRole('button', { name: 'Unpair' }))
    expect(screen.getByText('Unpair this browser?')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Yes, unpair' }))

    expect(onUnpair).toHaveBeenCalledExactlyOnceWith()
  })

  it('should unpair once when Yes is clicked twice', async () => {
    const { onUnpair, user } = renderConnection()
    await user.click(screen.getByRole('button', { name: 'Unpair' }))

    await user.dblClick(screen.getByRole('button', { name: 'Yes, unpair' }))

    expect(onUnpair).toHaveBeenCalledOnce()
    expect(screen.getByRole('button', { name: 'Yes, unpair' })).toHaveAttribute('aria-disabled', 'true')
  })

  it('should move focus into the unpair question', async () => {
    const { user } = renderConnection()

    await user.click(screen.getByRole('button', { name: 'Unpair' }))

    expect(screen.getByRole('group', { name: 'Unpair this browser?' })).toContainElement(document.activeElement as HTMLElement)
  })

  it('should return focus to Unpair after Cancel', async () => {
    const { user } = renderConnection()
    await user.click(screen.getByRole('button', { name: 'Unpair' }))

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByRole('button', { name: 'Unpair' })).toHaveFocus()
  })

  it('should warn that the pairing may be cleared when storage is not persisted', () => {
    renderConnection({ isPersisted: false })

    expect(screen.getByText(PERSIST_WARNING)).toBeInTheDocument()
  })

  it('should not warn when storage is persisted', () => {
    renderConnection({ isPersisted: true })

    expect(screen.queryByText(PERSIST_WARNING)).not.toBeInTheDocument()
  })
})
