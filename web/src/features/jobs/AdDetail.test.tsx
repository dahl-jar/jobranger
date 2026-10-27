import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { AdDetail } from '@/features/jobs/AdDetail'
import { ADS_SNAPSHOT, APPLIED_AD, CONFIG_SNAPSHOT, DATED_AD } from '@/features/jobs/goldenPayloads'
import type { ActiveAd } from '@/features/relay/contract/payload.gen'

const SECTION_NAMES = ['What the role is about', 'What the ad asks for', 'Why it fits you', 'Gaps']

function renderDetail(ad: ActiveAd | undefined, { from, error, onMark = vi.fn() }: { from?: unknown; error?: string; onMark?: () => void } = {}) {
  render(
    <MemoryRouter>
      <AdDetail
        ad={ad}
        today={ADS_SNAPSHOT.as_of}
        roles={CONFIG_SNAPSHOT.roles}
        from={from}
        disabled={false}
        error={error}
        onMark={onMark}
        onUnmark={vi.fn()}
      />
    </MemoryRouter>,
  )
}

describe('AdDetail', () => {
  it("should link Open on source to the ad's own URL", () => {
    renderDetail(DATED_AD)

    const link = screen.getByRole('link', { name: 'Open on FINN' })

    expect(link).toHaveAttribute('href', DATED_AD.url)
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it.each([
    ['today', 'Back to Today', '/app/today'],
    ['all', 'Back to All', '/app/all'],
    ['hidden', 'Back to Active', '/app/active'],
  ])('should go back from %s with %s', (from, name, href) => {
    renderDetail(DATED_AD, { from })

    expect(screen.getByRole('link', { name })).toHaveAttribute('href', href)
  })

  it('should send the mark for the open ad', async () => {
    const user = userEvent.setup()
    const onMark = vi.fn()
    renderDetail(DATED_AD, { onMark })

    await user.click(screen.getByRole('button', { name: 'I applied' }))

    expect(onMark).toHaveBeenCalledWith(DATED_AD.id, 'applied')
  })

  it('should say why a mark was not saved', () => {
    renderDetail(DATED_AD, { error: 'x' })

    expect(screen.getByRole('alert')).toHaveTextContent('Not saved: x')
  })

  it('should show every section of a fully checked ad', () => {
    renderDetail(DATED_AD)

    for (const name of SECTION_NAMES) expect(screen.getByRole('heading', { name })).toBeInTheDocument()
    expect(screen.getByText('No Airflow in the profile')).toBeInTheDocument()
  })

  it('should leave out sections the ad has nothing for', () => {
    renderDetail(APPLIED_AD)

    expect(screen.getByRole('heading', { name: 'Why it fits you' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'What the role is about' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'What the ad asks for' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Gaps' })).not.toBeInTheDocument()
  })

  it('should say the ad is gone when it left the list', () => {
    renderDetail(undefined)

    expect(screen.getByText('This ad is no longer in your list.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'See Active' })).toHaveAttribute('href', '/app/active')
  })
})
