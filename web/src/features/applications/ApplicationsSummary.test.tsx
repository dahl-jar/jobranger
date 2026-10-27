import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ApplicationsSummary } from '@/features/applications/ApplicationsSummary'
import { foundApplication, handApplication, pinTimeZone, removedApplication, TODAY } from '@/features/applications/testData'
import type { ApplicationStatus } from '@/features/relay/contract/payload.gen'

const withStatus = (status: ApplicationStatus, id: number) => ({ ...foundApplication, id, status })

describe('ApplicationsSummary', () => {
  pinTimeZone()

  it('should count applications sent since the first one', () => {
    render(<ApplicationsSummary applications={[foundApplication, handApplication]} today={TODAY} />)

    expect(screen.getByText('sent since 1 Oct').previousElementSibling).toHaveTextContent('2')
  })

  it('should leave removed applications out', () => {
    render(<ApplicationsSummary applications={[foundApplication, removedApplication]} today={TODAY} />)

    expect(screen.getByText('sent since today').previousElementSibling).toHaveTextContent('1')
  })

  it('should count offers and accepted ones as interviews', () => {
    render(
      <ApplicationsSummary
        applications={[withStatus('waiting', 1), withStatus('interview', 2), withStatus('offer', 3), withStatus('accepted', 4), withStatus('rejected', 5)]}
        today={TODAY}
      />,
    )

    expect(screen.getByRole('img')).toHaveAccessibleName('1 waiting, 1 rejected, 3 interviews')
    expect(screen.getByText('Interviews').nextElementSibling).toHaveTextContent('3')
  })

  it('should say so when there are no applications', () => {
    render(<ApplicationsSummary applications={[removedApplication]} today={TODAY} />)

    expect(screen.getByText('No applications yet.')).toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('should count from the earliest application whatever the order', () => {
    render(<ApplicationsSummary applications={[handApplication, foundApplication]} today={TODAY} />)

    expect(screen.getByText('sent since 1 Oct')).toBeInTheDocument()
  })

  it('should say yesterday for an application sent yesterday', () => {
    render(<ApplicationsSummary applications={[{ ...foundApplication, applied_at: '2026-10-11T09:00:00Z' }]} today={TODAY} />)

    expect(screen.getByText('sent since yesterday')).toBeInTheDocument()
  })

  it('should label each count in the legend', () => {
    render(<ApplicationsSummary applications={[foundApplication, handApplication]} today={TODAY} />)

    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual(['Waiting1', 'Rejected0', 'Interviews1'])
  })

  it('should leave out the application waiting for removal', () => {
    render(<ApplicationsSummary applications={[foundApplication, handApplication]} pendingId={handApplication.id} today={TODAY} />)

    expect(screen.getByText('sent since today').previousElementSibling).toHaveTextContent('1')
  })

  it('should size the bar by waiting, interviews and rejected counts', () => {
    render(
      <ApplicationsSummary
        applications={[withStatus('waiting', 1), withStatus('waiting', 2), withStatus('offer', 3), withStatus('rejected', 4), withStatus('rejected', 5), withStatus('rejected', 6)]}
        today={TODAY}
      />,
    )

    expect([...screen.getByRole('img').children].map((part) => (part instanceof HTMLElement ? part.style.flexGrow : ''))).toEqual(['2', '1', '3'])
  })
})
