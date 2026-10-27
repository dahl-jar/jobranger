import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { AllTable } from '@/features/jobs/AllTable'
import { detailPath } from '@/features/jobs/paths'
import { ADS_SNAPSHOT, APPLIED_AD, CLOSED_AD, DATED_AD, YEARS_HIDDEN_AD } from '@/features/jobs/goldenPayloads'
import { renderListRoute } from '@/features/jobs/testRoutes'

function renderTable(snapshot = ADS_SNAPSHOT) {
  render(
    <MemoryRouter>
      <AllTable snapshot={snapshot} />
    </MemoryRouter>,
  )
}

describe('AllTable', () => {
  it('should open active ads from their role', () => {
    renderTable()

    expect(screen.getByRole('link', { name: DATED_AD.title })).toHaveAttribute('href', detailPath(DATED_AD.id))
  })

  it('should show a hidden ad by its reason without a date', () => {
    renderTable()

    const row = screen.getByText(`Hidden: ${YEARS_HIDDEN_AD.reason}`).closest('[role="row"]')
    expect(row?.firstElementChild).toBeEmptyDOMElement()
  })

  it('should list active ads soonest deadline first', () => {
    renderTable({ ...ADS_SNAPSHOT, active: [APPLIED_AD, DATED_AD] })

    expect(screen.getAllByRole('link').map((link) => link.textContent)).toEqual([DATED_AD.title, APPLIED_AD.title])
  })

  it('should date a closed ad by the day it closed', () => {
    renderTable()

    const row = screen.getByText(CLOSED_AD.title).closest('[role="row"]')
    if (!(row instanceof HTMLElement)) throw new Error(`no row holds ${CLOSED_AD.title}`)
    expect(within(row).getAllByRole('cell')[0]).toHaveTextContent('8 Oct')
  })

  it('should give closed ads why they closed', () => {
    renderTable()

    expect(screen.getByText('Closed: Removed from FINN')).toBeInTheDocument()
  })

  it('should leave hidden and closed ads without a link', () => {
    renderTable()

    expect(screen.getAllByRole('link')).toHaveLength(ADS_SNAPSHOT.active.length)
    expect(screen.getByText(CLOSED_AD.title)).toBeInTheDocument()
  })

  it('should count every row it shows', () => {
    renderTable()

    expect(screen.getByText('Showing 5. Click a match to open it.')).toBeInTheDocument()
  })

  it('should open the detail page as coming from All', async () => {
    const user = userEvent.setup()
    renderListRoute('/app/all', <AllTable snapshot={ADS_SNAPSHOT} />)

    await user.click(screen.getByRole('link', { name: DATED_AD.title }))

    expect(screen.getByText('opened from all')).toBeInTheDocument()
  })
})
