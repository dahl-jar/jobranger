import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { PairConnected } from '@/features/pairing/PairConnected'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Status } from '@/features/relay/contract/payload.gen'

const STATUS = payloadFixtures.status as Status

function renderFacts(status: Status | undefined) {
  render(
    <MemoryRouter>
      <PairConnected status={status} onUnpair={() => undefined} />
    </MemoryRouter>,
  )
}

function fact(label: string): string {
  return screen.getByText(label).nextElementSibling?.textContent ?? ''
}

describe('PairConnected', () => {
  it('should show the machine and version', () => {
    renderFacts(STATUS)

    expect([fact('Machine'), fact('Version')]).toEqual(['laptop', 'jobranger 0.1.0'])
  })

  it('should show the last run with its match count', () => {
    renderFacts(STATUS)

    expect(fact('Last run')).toMatch(/^\d{1,2} Oct, \d{2}:\d{2}, 6 matches$/)
  })

  it('should say 1 match for a single match', () => {
    renderFacts({ ...STATUS, last_run: { ...(STATUS.last_run as NonNullable<Status['last_run']>), matches: 1 } })

    expect(fact('Last run')).toMatch(/, 1 match$/)
  })

  it('should say No run yet before the first run', () => {
    renderFacts({ ...STATUS, last_run: null })

    expect(fact('Last run')).toBe('No run yet')
  })

  it('should show no facts before the first status', () => {
    renderFacts(undefined)

    expect(screen.queryByText('Machine')).not.toBeInTheDocument()
  })
})
