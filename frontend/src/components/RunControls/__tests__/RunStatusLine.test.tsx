import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RunStatusLine } from '@/components/RunControls/RunStatusLine'
import styles from '@/components/RunControls/RunControls.module.css'
import { payloadFixtures } from '@/contract/fixtures.gen'
import type { Status } from '@/contract/payload.gen'

const status = payloadFixtures['status']
const lastRun = { ...status.last_run, finished_at: new Date(2026, 9, 12, 3, 4).toISOString() }
const nextRunAt = new Date(2026, 9, 12, 15, 0).toISOString()
const idle: Status = { ...status, running: null, last_run: lastRun, next_run_at: nextRunAt }

describe('RunStatusLine', () => {
  it.each([
    ['a run is going', status, 'Running now'],
    ['the last and next run are known', idle, 'Read 812 ads at 03:04, next run 15:00'],
    ['no run times are set', { ...idle, next_run_at: null }, 'Read 812 ads at 03:04, no run times set'],
    ['nothing has run yet', { ...idle, last_run: null }, 'Next run 15:00'],
    ['nothing has run and no times are set', payloadFixtures['status.idle'], 'No run times set'],
  ])('should describe runs when %s', (_, runStatus, text) => {
    render(<RunStatusLine status={runStatus} offline={false} />)

    expect(screen.getByText(text)).toBeInTheDocument()
  })

  it('should style the run line as offline when the machine is offline', () => {
    render(<RunStatusLine status={status} offline />)

    expect(screen.getByText('Machine offline').className).toBe(`${styles.line} ${styles.offline}`)
  })

  it('should show nothing before the status arrives', () => {
    const { container } = render(<RunStatusLine status={undefined} offline={false} />)

    expect(container).toBeEmptyDOMElement()
  })
})
