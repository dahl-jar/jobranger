import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import {
  ADS_SNAPSHOT,
  CONFIG_SNAPSHOT,
  OTHER_HIDDEN_AD,
  TRUNCATED_SNAPSHOT,
  YEARS_HIDDEN_AD,
} from '@/features/jobs/goldenPayloads'
import { HiddenTable } from '@/features/jobs/HiddenTable'
import type { AdsSnapshot, HiddenKind } from '@/features/relay/contract/payload.gen'

const LOOSENS = 'Show anyway also loosens this limit one step from the next run.'
const ONLY_THIS = 'Show anyway shows only this ad.'
const ALL_PILL = `All ${ADS_SNAPSHOT.hidden_total}`
const NO_MOVES: ReadonlySet<string> = new Set()

function renderTable(
  snapshot: AdsSnapshot,
  {
    reason,
    movedIds = NO_MOVES,
    disabled = false,
  }: { reason?: HiddenKind | undefined; movedIds?: ReadonlySet<string>; disabled?: boolean } = {},
) {
  const callbacks = { onReasonChange: vi.fn(), onShowAnyway: vi.fn() }
  render(
    <HiddenTable
      snapshot={snapshot}
      reason={reason}
      bands={CONFIG_SNAPSHOT.targets.years}
      movedIds={movedIds}
      disabled={disabled}
      {...callbacks}
    />,
  )
  return callbacks
}

function rowOf(company: string) {
  const row = screen.getByText(company).closest('[role="row"]')
  if (!(row instanceof HTMLElement)) throw new Error(`no row holds ${company}`)
  return row
}

describe('HiddenTable', () => {
  it.each([
    ['masters', ONLY_THIS],
    ['role', ONLY_THIS],
    ['other', ONLY_THIS],
    ['years', LOOSENS],
    ['senior', LOOSENS],
    ['place', LOOSENS],
  ] as const)('should explain what Show anyway does on a %s row', (kind, text) => {
    renderTable({ ...ADS_SNAPSHOT, hidden: [{ ...YEARS_HIDDEN_AD, kind }] })

    expect(within(rowOf(YEARS_HIDDEN_AD.company)).getByText(text)).toBeInTheDocument()
  })

  it.each([
    [true, 'still hidden', NO_MOVES, 'Show anyway'],
    [false, 'moved to Active', new Set([YEARS_HIDDEN_AD.id]), 'Moved to Active'],
  ])('should ask to set show anyway to %s for an ad %s', async (on, _, movedIds, name) => {
    const user = userEvent.setup()
    const { onShowAnyway } = renderTable(ADS_SNAPSHOT, { movedIds })

    await user.click(within(rowOf(YEARS_HIDDEN_AD.company)).getByRole('button', { name }))

    expect(onShowAnyway).toHaveBeenCalledWith(YEARS_HIDDEN_AD.id, on)
  })

  it('should say an ad shown anyway moved to Active', () => {
    renderTable(ADS_SNAPSHOT, { movedIds: new Set([YEARS_HIDDEN_AD.id]) })

    expect(screen.getByRole('button', { name: 'Moved to Active' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('should lock Show anyway when edits are off', () => {
    renderTable(ADS_SNAPSHOT, { disabled: true })

    expect(within(rowOf(YEARS_HIDDEN_AD.company)).getByRole('button', { name: 'Show anyway' })).toBeDisabled()
  })

  it('should describe each Show anyway button by its ad title', () => {
    renderTable(ADS_SNAPSHOT)

    expect(within(rowOf(OTHER_HIDDEN_AD.company)).getByRole('button')).toHaveAccessibleDescription(OTHER_HIDDEN_AD.title)
  })

  it('should list only ads hidden for the picked reason', () => {
    renderTable(ADS_SNAPSHOT, { reason: 'other' })

    expect(screen.getByText(OTHER_HIDDEN_AD.company)).toBeInTheDocument()
    expect(screen.queryByText(YEARS_HIDDEN_AD.company)).not.toBeInTheDocument()
  })

  it.each([
    ['Need 3+ yrs 1', undefined, 'years'],
    [ALL_PILL, 'years', undefined],
  ] as const)('should pick %s from its pill', async (name, reason, picked) => {
    const user = userEvent.setup()
    const { onReasonChange } = renderTable(ADS_SNAPSHOT, { reason })

    await user.click(screen.getByRole('button', { name }))

    expect(onReasonChange).toHaveBeenCalledWith(picked)
  })

  it.each([
    [undefined, [ALL_PILL]],
    ['other', ['Other reasons 1']],
  ] as const)('should press only the pill of reason %s', (reason, pressed) => {
    renderTable(ADS_SNAPSHOT, { reason })

    const pills = screen.getAllByRole('button', { pressed: true }).filter((button) => button.closest('[role="table"]') === null)
    expect(pills.map((pill) => pill.textContent)).toEqual(pressed)
  })

  it.each([
    [undefined, 'Showing 2 of 41.'],
    ['years', 'Showing 1 of 9.'],
  ] as const)('should count shown rows against the total for reason %s', (reason, text) => {
    renderTable(TRUNCATED_SNAPSHOT, { reason })

    expect(screen.getByText(text, { exact: false })).toHaveTextContent(`${text} "Show anyway" moves the ad to Active.`)
  })
})
