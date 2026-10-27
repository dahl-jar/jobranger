import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ADS_SNAPSHOT, APPLIED_AD, CONFIG_SNAPSHOT, DATED_AD } from '@/features/jobs/goldenPayloads'
import { JobsView } from '@/features/jobs/JobsView'
import type { JobList } from '@/features/jobs/selectors'
import styles from '@/features/jobs/JobsView.module.css'
import type { AdsSnapshot, ConfigSnapshot } from '@/features/relay/contract/payload.gen'

const HIDDEN_LINE = new RegExp(`^${ADS_SNAPSHOT.hidden_total} hidden:`)
function byClass(root: Element, className: string | undefined) {
  if (className === undefined) throw new Error('JobsView.module.css has no such class')
  return root.getElementsByClassName(className)
}

const EMPTY_TEXT = 'No matches here for the roles you picked.'
const EARLIER_SNAPSHOT = { ...ADS_SNAPSHOT, active: [{ ...DATED_AD, today: false }, APPLIED_AD] }

function renderView(
  list: JobList,
  snapshot: AdsSnapshot = ADS_SNAPSHOT,
  config: ConfigSnapshot | null = CONFIG_SNAPSHOT,
  errors: Readonly<Record<string, string>> = {},
) {
  render(
    <MemoryRouter>
      <JobsView
        list={list}
        snapshot={snapshot}
        config={config ?? undefined}
        reason={undefined}
        movedIds={new Set()}
        applications={3}
        errors={errors}
        disabled={false}
        onMark={vi.fn()}
        onUnmark={vi.fn()}
        onShowAnyway={vi.fn()}
        onReasonChange={vi.fn()}
      />
    </MemoryRouter>,
  )
}

describe('JobsView', () => {
  it.each([
    ['hidden', 'Hidden ads'],
    ['closed', 'Closed ads'],
    ['all', 'All ads'],
  ] as const)('should show the %s list as its table', (list, name) => {
    renderView(list)

    expect(screen.getByRole('table', { name })).toBeInTheDocument()
  })

  it('should show the matches of the active list', () => {
    renderView('active')

    expect(screen.getByRole('heading', { name: DATED_AD.title })).toBeInTheDocument()
    expect(screen.getByText('Open matches, soonest deadline first.')).toBeInTheDocument()
  })

  it('should say so when a list has no matches', () => {
    renderView('today', { ...ADS_SNAPSHOT, active: [] })

    expect(screen.getByText(EMPTY_TEXT)).toBeInTheDocument()
  })

  it('should sum up hidden ads under Today', () => {
    renderView('today')

    expect(screen.getByText(HIDDEN_LINE)).toBeInTheDocument()
  })

  it('should keep the hidden summary off the Active list', () => {
    renderView('active')

    expect(screen.queryByText(HIDDEN_LINE)).not.toBeInTheDocument()
  })

  it('should leave an ad found earlier off Today', () => {
    renderView('today', EARLIER_SNAPSHOT)

    expect(screen.queryByRole('heading', { name: DATED_AD.title })).not.toBeInTheDocument()
  })

  it('should list an ad found earlier on Active', () => {
    renderView('active', EARLIER_SNAPSHOT)

    expect(screen.getByRole('heading', { name: DATED_AD.title })).toBeInTheDocument()
  })

  it('should show a save error on the ad it belongs to', () => {
    renderView('today', ADS_SNAPSHOT, CONFIG_SNAPSHOT, { [DATED_AD.id]: 'no answer from your machine.' })

    expect(within(screen.getByRole('article', { name: DATED_AD.title })).getByRole('alert')).toBeInTheDocument()
    expect(within(screen.getByRole('article', { name: APPLIED_AD.title })).queryByRole('alert')).not.toBeInTheDocument()
  })

  it('should show matches before the config arrives', () => {
    renderView('today', ADS_SNAPSHOT, null)

    expect(screen.getByText(/need more yrs/)).toBeInTheDocument()
    expect(screen.getAllByText('Other')).toHaveLength(ADS_SNAPSHOT.active.length)
  })

  it('should give the Today list no note', () => {
    renderView('today')

    expect(byClass(document.body, styles.note)).toHaveLength(0)
  })
})
