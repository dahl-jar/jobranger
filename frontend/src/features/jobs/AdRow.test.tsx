import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AdRow } from '@/features/jobs/AdRow'
import styles from '@/features/jobs/AdRow.module.css'
import { detailPath } from '@/features/jobs/paths'
import { ADS_SNAPSHOT, APPLIED_AD, CONFIG_SNAPSHOT, DATED_AD } from '@/features/jobs/goldenPayloads'
import { renderListRoute } from '@/features/jobs/testRoutes'
import type { ActiveAd } from '@/features/relay/contract/payload.gen'

const TODAY = ADS_SNAPSHOT.as_of

function byClass(root: Element, className: string | undefined) {
  if (className === undefined) throw new Error('AdRow.module.css has no such class')
  return root.getElementsByClassName(className)
}

function renderRow(ad: ActiveAd, { error, onMark = vi.fn(), disabled = false }: { error?: string; onMark?: () => void; disabled?: boolean } = {}) {
  return renderListRoute(
    '/app/today',
    <AdRow
      ad={ad}
      today={TODAY}
      roles={CONFIG_SNAPSHOT.roles}
      from="today"
      disabled={disabled}
      error={error}
      onMark={onMark}
      onUnmark={vi.fn()}
    />,
  ).router
}

describe('AdRow', () => {
  it('should open the detail page with the list it came from', async () => {
    const user = userEvent.setup()
    const router = renderRow(DATED_AD)

    await user.click(screen.getByRole('link', { name: DATED_AD.title }))

    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(detailPath(DATED_AD.id))
    expect(screen.getByText('opened from today')).toBeInTheDocument()
  })

  it('should say a new ad is new', () => {
    renderRow(APPLIED_AD)

    expect(screen.getByText('New, just now')).toBeInTheDocument()
  })

  it('should not call an older ad new', () => {
    renderRow(DATED_AD)

    expect(screen.getByRole('heading', { name: DATED_AD.title })).toBeInTheDocument()
    expect(screen.queryByText('New, just now')).not.toBeInTheDocument()
  })

  it.each([
    ['applied', null, 'Applied'],
    ['applied', '2026-10-12T07:45:00Z', 'Applied today'],
    ['applied', '2026-10-09T09:00:00Z', 'Applied 9 Oct'],
    ['skipped', '2026-10-09T09:00:00Z', 'Skipped'],
  ] as const)('should label a %s mark from %s as %s', (mark, markedAt, label) => {
    renderRow({ ...DATED_AD, mark, marked_at: markedAt })

    expect(screen.getByText(label, { ignore: 'button' })).toBeInTheDocument()
  })

  it('should show the area from the role catalogue', () => {
    renderRow(DATED_AD)

    expect(screen.getByText('Data engineering')).toBeInTheDocument()
  })

  it('should say why a mark was not saved', () => {
    renderRow(DATED_AD, { error: 'no answer from your machine.' })

    expect(screen.getByRole('alert')).toHaveTextContent('Not saved: no answer from your machine.')
  })

  it('should send the mark for this ad', async () => {
    const user = userEvent.setup()
    const onMark = vi.fn()
    renderRow(DATED_AD, { onMark })

    await user.click(screen.getByRole('button', { name: 'Skip' }))

    expect(onMark).toHaveBeenCalledWith(DATED_AD.id, 'skipped')
  })

  it('should disable the marks when edits are off', () => {
    renderRow(DATED_AD, { disabled: true })

    expect(screen.getByRole('button', { name: 'Skip' })).toBeDisabled()
  })

  it('should prefer the summary over the match reason', () => {
    renderRow(DATED_AD)

    expect(screen.getByText(DATED_AD.summary)).toBeInTheDocument()
  })

  it('should show the match reason when the summary is empty', () => {
    renderRow(APPLIED_AD)

    expect(screen.getByText(APPLIED_AD.reason)).toBeInTheDocument()
  })

  it("should give each of the ad's checks its state in words", () => {
    renderRow(DATED_AD)

    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Python met',
      'Kafka partly',
      'Airflow missing',
    ])
  })

  it('should leave the checks out when the ad has none', () => {
    renderRow(APPLIED_AD)

    expect(screen.queryByRole('list')).not.toBeInTheDocument()
  })

  it('should open the detail page from Details with the list it came from', async () => {
    const user = userEvent.setup()
    renderRow(DATED_AD)

    await user.click(screen.getByRole('link', { name: 'Details' }))

    expect(screen.getByText('opened from today')).toBeInTheDocument()
  })

  it('should note an ad shown anyway by you', () => {
    renderRow({ ...APPLIED_AD, shown_anyway: true })

    expect(screen.getByText('shown by you')).toBeInTheDocument()
  })

  it('should leave the note off an ad not shown anyway', () => {
    renderRow({ ...APPLIED_AD, shown_anyway: false })

    expect(screen.queryByText('shown by you')).not.toBeInTheDocument()
  })

  it('should show the place when the ad names one', () => {
    renderRow(DATED_AD)

    expect(screen.getByText(DATED_AD.place)).toBeInTheDocument()
  })

  it('should leave the place out when the ad names none', () => {
    renderRow(APPLIED_AD)

    const facts = screen.getByText('NAV').parentElement?.children ?? []

    expect(Array.from(facts, (fact) => fact.textContent)).toEqual(['Other', 'NAV'])
  })

  it.each(['skipped', 'not_for_me'] as const)('should dim a row marked %s', (mark) => {
    renderRow({ ...DATED_AD, mark })

    expect(screen.getByRole('article')).toHaveClass(styles.dimmed ?? '')
  })

  it.each(['applied', null] as const)('should keep a row marked %s undimmed', (mark) => {
    renderRow({ ...DATED_AD, mark })

    expect(screen.getByRole('article')).not.toHaveClass(styles.dimmed ?? '')
  })

  it('should show no mark badge on an unmarked ad', () => {
    renderRow(DATED_AD)

    expect(byClass(screen.getByRole('article'), styles.markLabel)).toHaveLength(0)
  })

  it('should show no error when the mark was saved', () => {
    renderRow(DATED_AD)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
