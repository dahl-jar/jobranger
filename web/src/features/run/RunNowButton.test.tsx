import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { RunNowButton } from '@/features/run/RunNowButton'

describe('RunNowButton', () => {
  it('should start a run on click', async () => {
    const onRun = vi.fn()
    render(<RunNowButton running={false} editsDisabled={false} onRun={onRun} />)

    await userEvent.click(screen.getByRole('button', { name: 'Run now' }))

    expect(onRun).toHaveBeenCalledOnce()
  })

  it('should disable Run now while the machine is offline', () => {
    render(<RunNowButton running={false} editsDisabled onRun={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Run now' })).toBeDisabled()
  })

  it('should disable Run now while a start is in flight', () => {
    render(<RunNowButton running={false} editsDisabled={false} pending onRun={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Run now' })).toBeDisabled()
  })

  it('should say Running while a run is going', () => {
    render(<RunNowButton running editsDisabled={false} onRun={vi.fn()} />)

    expect(screen.getByRole('button')).toHaveAccessibleName('Running')
  })

  it('should disable the button while a run is going', () => {
    render(<RunNowButton running editsDisabled={false} onRun={vi.fn()} />)

    expect(screen.getByRole('button')).toBeDisabled()
  })
})
