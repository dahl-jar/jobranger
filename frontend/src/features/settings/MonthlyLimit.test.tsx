import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { MonthlyLimit } from '@/features/settings/MonthlyLimit'

const LIMIT_USD = payloadFixtures['config.snapshot'].model.monthly_limit_usd
const MONTH_USD = payloadFixtures['usage.snapshot'].month_usd

describe('MonthlyLimit', () => {
  it.each([
    { pick: '$5.00', sent: 5 },
    { pick: 'No limit', sent: null },
  ])('should send $sent when $pick is picked', async ({ pick, sent }) => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<MonthlyLimit limitUsd={LIMIT_USD} monthUsd={MONTH_USD} onChange={onChange} />)

    await user.click(screen.getByRole('radio', { name: pick }))

    expect(onChange).toHaveBeenCalledExactlyOnceWith(sent)
  })

  it('should offer the set limit when it is not a preset', () => {
    render(<MonthlyLimit limitUsd={LIMIT_USD} monthUsd={MONTH_USD} onChange={vi.fn()} />)

    expect(screen.getByRole('radio', { name: '$20.00' })).toBeChecked()
  })

  it('should place a set limit between the presets', () => {
    render(<MonthlyLimit limitUsd={4} monthUsd={MONTH_USD} onChange={vi.fn()} />)

    expect(screen.getAllByRole('radio').map((radio) => radio.parentElement?.textContent)).toEqual([
      'No limit',
      '$1.00',
      '$3.00',
      '$4.00',
      '$5.00',
      '$10.00',
    ])
  })

  it('should check No limit when there is no limit', () => {
    render(<MonthlyLimit limitUsd={null} monthUsd={MONTH_USD} onChange={vi.fn()} />)

    expect(screen.getByRole('radio', { name: 'No limit' })).toBeChecked()
    expect(screen.getAllByRole('radio')).toHaveLength(5)
  })

  it.each([
    { limitUsd: LIMIT_USD, note: '12% used. AI checks pause at the limit until next month.' },
    { limitUsd: null, note: 'No limit set.' },
    { limitUsd: 0, note: '100% used. AI checks pause at the limit until next month.' },
  ])('should say "$note" for limit $limitUsd', ({ limitUsd, note }) => {
    render(<MonthlyLimit limitUsd={limitUsd} monthUsd={MONTH_USD} onChange={vi.fn()} />)

    expect(screen.getByText(note)).toBeInTheDocument()
  })

  it('should stop at 100% used when the month passes the limit', () => {
    render(<MonthlyLimit limitUsd={1} monthUsd={MONTH_USD} onChange={vi.fn()} />)

    expect(screen.getByText('100% used. AI checks pause at the limit until next month.')).toBeInTheDocument()
  })
})
