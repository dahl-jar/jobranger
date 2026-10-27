import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AdActions } from '@/features/jobs/AdActions'

const ACTION_NAMES = ['I applied', 'Skip', 'Not for me', 'Undo']

describe('AdActions', () => {
  it('should send the picked mark', async () => {
    const user = userEvent.setup()
    const onMark = vi.fn()
    render(<AdActions adId="ad-1" mark={null} disabled={false} describedBy="title" onMark={onMark} onUnmark={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Not for me' }))

    expect(onMark).toHaveBeenCalledWith('ad-1', 'not_for_me')
  })

  it('should remove the mark on Undo', async () => {
    const user = userEvent.setup()
    const onUnmark = vi.fn()
    render(<AdActions adId="ad-1" mark="skipped" disabled={false} describedBy="title" onMark={vi.fn()} onUnmark={onUnmark} />)

    await user.click(screen.getByRole('button', { name: 'Undo' }))

    expect(onUnmark).toHaveBeenCalledWith('ad-1')
  })

  it('should offer Undo only when the ad has a mark', () => {
    render(<AdActions adId="ad-1" mark={null} disabled={false} describedBy="title" onMark={vi.fn()} onUnmark={vi.fn()} />)

    expect(screen.queryByRole('button', { name: 'Undo' })).not.toBeInTheDocument()
  })

  it('should disable every action when edits are off', () => {
    render(<AdActions adId="ad-1" mark="applied" disabled describedBy="title" onMark={vi.fn()} onUnmark={vi.fn()} />)

    for (const name of ACTION_NAMES) expect(screen.getByRole('button', { name })).toBeDisabled()
  })
})
