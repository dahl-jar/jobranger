import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { TRUNCATED_SNAPSHOT } from '@/features/jobs/goldenPayloads'
import { ListTabs } from '@/features/jobs/ListTabs'
import { listCounts } from '@/features/jobs/selectors'

const COUNTS = listCounts(TRUNCATED_SNAPSHOT)

describe('ListTabs', () => {
  it.each([
    [`Today ${COUNTS.today}`, '/app/today'],
    [`Active ${COUNTS.active}`, '/app/active'],
    [`Hidden ${COUNTS.hidden}`, '/app/hidden'],
    [`Closed ${COUNTS.closed}`, '/app/closed'],
    [`All ${COUNTS.all}`, '/app/all'],
    ['Applications 3', '/app/applications'],
  ])('should link %s to %s', (name, href) => {
    render(
      <MemoryRouter>
        <ListTabs counts={COUNTS} applications={3} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name })).toHaveAttribute('href', href)
  })

  it('should leave the Applications count out until it is known', () => {
    render(
      <MemoryRouter>
        <ListTabs counts={COUNTS} applications={undefined} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Applications' }).textContent).toBe('Applications')
  })
})
