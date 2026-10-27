import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApplicationsTable } from '@/features/applications/ApplicationsTable'
import styles from '@/features/applications/ApplicationsTable.module.css'
import type { SortState } from '@/features/applications/sortApplications'
import { foundApplication, handApplication, pinTimeZone } from '@/features/applications/testData'

const TODAY = '2026-10-12'

function renderTable(sort: SortState = { key: 'applied', dir: 'desc' }) {
  const handlers = { onSort: vi.fn(), onOpen: vi.fn(), onStatusChange: vi.fn() }
  render(
    <ApplicationsTable
      applications={[foundApplication, handApplication]}
      sort={sort}
      today={TODAY}
      editsDisabled={false}
      {...handlers}
    />,
  )
  return handlers
}

const rowOf = (company: string) => {
  const row = screen.getByRole('button', { name: company }).closest('[role="row"]')
  if (!(row instanceof HTMLElement)) throw new Error(`no row for ${company}`)
  return row
}

describe('ApplicationsTable', () => {
  pinTimeZone()

  it('should open the application when its row is clicked', async () => {
    const user = userEvent.setup()
    const { onOpen } = renderTable()

    await user.click(within(rowOf(foundApplication.company)).getByText('Today'))

    expect(onOpen).toHaveBeenCalledExactlyOnceWith(foundApplication.id)
  })

  it('should open the application once from the company button', async () => {
    const user = userEvent.setup()
    const { onOpen } = renderTable()
    screen.getByRole('button', { name: handApplication.company }).focus()

    await user.keyboard('{Enter}')

    expect(onOpen).toHaveBeenCalledExactlyOnceWith(handApplication.id)
  })

  it('should change the status without opening the row', async () => {
    const user = userEvent.setup()
    const { onOpen, onStatusChange } = renderTable()
    await user.click(within(rowOf(foundApplication.company)).getByRole('button', { name: 'Status: Waiting' }))

    await user.click(screen.getByRole('menuitemradio', { name: 'Offer' }))

    expect(onStatusChange).toHaveBeenCalledExactlyOnceWith(foundApplication.id, 'offer')
    expect(onOpen).not.toHaveBeenCalled()
  })

  it.each([
    { dir: 'asc' as const, ariaSort: 'ascending' },
    { dir: 'desc' as const, ariaSort: 'descending' },
  ])('should mark only the sorted header as $ariaSort', ({ dir, ariaSort }) => {
    renderTable({ key: 'company', dir })

    expect(screen.getAllByRole('columnheader').map((header) => header.getAttribute('aria-sort'))).toEqual([
      ariaSort,
      'none',
      'none',
      'none',
      'none',
    ])
  })

  it('should show placeholders for a missing role and an empty note', () => {
    renderTable()

    expect(within(rowOf(foundApplication.company)).getByRole('button', { name: `Open note for ${foundApplication.company}` })).toHaveTextContent('Add a note')
    expect(within(rowOf(handApplication.company)).getByText('Role not set')).toBeInTheDocument()
    expect(within(rowOf(handApplication.company)).getByText('1 Oct')).toBeInTheDocument()
  })

  it('should label the columns', () => {
    renderTable()

    expect(screen.getAllByRole('columnheader').map((header) => header.textContent)).toEqual(['Company', 'Role', 'Applied', 'Status', 'Note'])
  })

  it.each([
    { label: 'Company', key: 'company' },
    { label: 'Role', key: 'role' },
    { label: 'Applied', key: 'applied' },
    { label: 'Status', key: 'status' },
    { label: 'Note', key: 'note' },
  ])('should ask to sort by $key from the $label header', async ({ label, key }) => {
    const user = userEvent.setup()
    const { onSort } = renderTable()

    await user.click(screen.getByRole('button', { name: label }))

    expect(onSort).toHaveBeenCalledExactlyOnceWith(key)
  })

  it('should class an unsorted header button without a sorted class', () => {
    renderTable()

    expect(screen.getByRole('button', { name: 'Company' }).className).toBe(styles.sortButton)
  })

  it('should class a filled note without the empty-note class', () => {
    renderTable()

    expect(screen.getByRole('button', { name: `Open note for ${handApplication.company}` }).className).toBe(styles.note)
  })

  it('should class the sorted header button as sorted', () => {
    renderTable({ key: 'company', dir: 'asc' })

    expect(screen.getByRole('button', { name: 'Company' }).className).toBe(`${styles.sortButton} ${styles.sorted}`)
  })

  it('should class an empty note as empty', () => {
    renderTable()

    expect(screen.getByRole('button', { name: `Open note for ${foundApplication.company}` }).className).toBe(`${styles.note} ${styles.noNote}`)
  })
})
