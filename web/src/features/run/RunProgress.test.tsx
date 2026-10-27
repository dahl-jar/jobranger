import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it, vi } from 'vitest'
import styles from '@/features/run/run.module.css'
import { RunProgress } from '@/features/run/RunProgress'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { RunProgress as RunProgressMessage } from '@/features/relay/contract/payload.gen'
import { Icon, type IconName } from '@/shared/ui/Icon'

const STEP_ICON_SIZE = 13

const progress = payloadFixtures['run.progress']

const everyStep: RunProgressMessage = {
  ...progress,
  sources: [
    { source: 'nav', state: 'done', found_new: 12 },
    { source: 'teamtailor', state: 'active', found_new: null },
    { source: 'finn', state: 'failed', found_new: null },
    { source: 'ashby', state: 'pending', found_new: null },
  ],
  model: { state: 'skipped', checked: 0, total: 0, results_by: null },
}

describe('RunProgress', () => {
  it('should set the progress bar value to the run percent', () => {
    render(<RunProgress progress={progress} editsDisabled={false} onCancel={vi.fn()} />)

    expect(screen.getByRole('progressbar', { name: 'Run progress' })).toHaveAttribute('aria-valuenow', '40')
  })

  it('should announce only the stage and the percent', () => {
    render(<RunProgress progress={progress} editsDisabled={false} onCancel={vi.fn()} />)

    expect(screen.getByRole('status').textContent).toBe('Reading Teamtailor40%')
  })

  it.each([
    ['NAV, 12 new', 'done', 'check'],
    ['Reading Teamtailor', 'active', 'running'],
    ['FINN failed', 'failed', 'close'],
    ['Ashby', 'pending', 'pending'],
    ['Paused at your monthly limit', 'skipped', 'dash'],
  ] as const)('should mark the step "%s" as %s', (text, state, icon: IconName) => {
    render(<RunProgress progress={everyStep} editsDisabled={false} onCancel={vi.fn()} />)

    const step = screen.getByText(text).closest('li')
    expect(step?.className).toBe(`${styles.step} ${styles[state]}`)
    expect(step?.querySelector('svg')?.outerHTML).toBe(renderToStaticMarkup(<Icon name={icon} size={STEP_ICON_SIZE} />))
  })

  it('should disable Cancel while a cancel is in flight', () => {
    render(<RunProgress progress={progress} editsDisabled={false} pending onCancel={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled()
  })

  it('should cancel the run by its id', async () => {
    const onCancel = vi.fn()
    render(<RunProgress progress={progress} editsDisabled={false} onCancel={onCancel} />)

    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalledWith(42)
  })

  it('should disable Cancel while the machine is offline', () => {
    render(<RunProgress progress={progress} editsDisabled onCancel={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled()
  })
})
