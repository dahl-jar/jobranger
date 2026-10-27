import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { ConfigSnapshot } from '@/features/relay/contract/payload.gen'
import { SearchSettings } from '@/features/settings/SearchSettings'

const CONFIG = payloadFixtures['config.snapshot']
const TWENTY_PLACES = Array.from({ length: 20 }, (_, index) => `Place ${index + 1}`)
const ALL_LEVELS = ['junior', 'mid', 'senior', 'lead', 'none']

function renderSearch(config: ConfigSnapshot = CONFIG) {
  const onPatch = vi.fn()
  render(<SearchSettings config={config} onPatch={onPatch} />)
  return { onPatch, user: userEvent.setup() }
}

function withTargets(targets: Partial<ConfigSnapshot['targets']>): ConfigSnapshot {
  return { ...CONFIG, targets: { ...CONFIG.targets, ...targets } }
}

describe('SearchSettings', () => {
  it.each([
    { group: 'Roles', timing: 'Applies now.' },
    { group: 'Level', timing: 'Applies from the next run.' },
    { group: 'Experience the ad asks for', timing: 'Applies from the next run.' },
    { group: "Master's degree", timing: 'Applies from the next run.' },
    { group: 'Places', timing: 'Applies now.' },
  ])('should say $group changes $timing', ({ group, timing }) => {
    renderSearch()

    expect(within(screen.getByRole('region', { name: group })).getByText(timing)).toBeInTheDocument()
  })

  it.each([
    { group: 'Level', options: ['All', 'Junior', 'Mid-level', 'Senior', 'Tech lead', 'No level stated'] },
    { group: 'Experience the ad asks for', options: ['All', '0–1 years', '1–3 years', '3–6 years', '6+ years'] },
  ])('should list the $group options', ({ group, options }) => {
    renderSearch()

    const region = within(screen.getByRole('region', { name: group }))
    expect(options.map((option) => region.getByRole('checkbox', { name: option }))).toHaveLength(options.length)
    expect(region.getAllByRole('checkbox')).toHaveLength(options.length)
  })

  it.each([
    { places: [], placeholder: 'Anywhere in Norway', hint: 'On, since no places are set' },
    { places: ['Bergen'], placeholder: 'Add a place', hint: 'Other cities show as Partial fit' },
  ])('should explain places $places', ({ places, placeholder, hint }) => {
    renderSearch(withTargets({ places }))

    expect(screen.getByRole('textbox', { name: 'Places' })).toHaveAttribute('placeholder', placeholder)
    expect(screen.getByRole('checkbox', { name: 'Open to moving' })).toHaveAccessibleDescription(hint)
  })

  it('should send the roles with a newly ticked role', async () => {
    const { onPatch, user } = renderSearch()

    await user.click(screen.getByRole('checkbox', { name: 'Frontend' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { subfields: ['backend', 'data_engineering', 'frontend'] } })
  })

  it('should send the roles without an unticked role', async () => {
    const { onPatch, user } = renderSearch()

    await user.click(screen.getByRole('checkbox', { name: 'Backend' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { subfields: ['data_engineering'] } })
  })

  it('should send the levels with a newly ticked level', async () => {
    const { onPatch, user } = renderSearch()

    await user.click(screen.getByRole('checkbox', { name: 'Senior' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { levels: ['junior', 'mid', 'senior'] } })
  })

  it('should pick every level when All is ticked', async () => {
    const { onPatch, user } = renderSearch()

    await user.click(within(screen.getByRole('region', { name: 'Level' })).getByRole('checkbox', { name: 'All' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { levels: ALL_LEVELS } })
  })

  it('should clear the levels when All is unticked', async () => {
    const { onPatch, user } = renderSearch(withTargets({ levels: ['junior', 'mid', 'senior', 'lead', 'none'] }))

    await user.click(within(screen.getByRole('region', { name: 'Level' })).getByRole('checkbox', { name: 'All' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { levels: [] } })
  })

  it('should send the experience bands with a newly ticked band', async () => {
    const { onPatch, user } = renderSearch()

    await user.click(screen.getByRole('checkbox', { name: '3–6 years' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { years: ['0-1', '1-3', '3-6'] } })
  })

  it('should tick the saved experience bands', () => {
    renderSearch()

    expect(screen.getByRole('checkbox', { name: '0–1 years' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: '1–3 years' })).toBeChecked()
  })

  it("should send the master's limit", async () => {
    const { onPatch, user } = renderSearch()

    await user.click(screen.getByRole('checkbox', { name: "Hide ads that require a master's" }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { hide_masters: false } })
  })

  it('should add "Mo i Rana" on Enter', async () => {
    const { onPatch, user } = renderSearch()

    await user.type(screen.getByRole('textbox', { name: 'Places' }), 'Mo i Rana{Enter}')

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { places: ['Bergen', 'Oslo', 'Mo i Rana'] } })
  })

  it('should cap a place at 80 characters', () => {
    renderSearch()

    expect(screen.getByRole('textbox', { name: 'Places' })).toHaveAttribute('maxlength', '80')
  })

  it('should refuse a 21st place', async () => {
    const { onPatch, user } = renderSearch(withTargets({ places: TWENTY_PLACES }))

    await user.type(screen.getByRole('textbox', { name: 'Places' }), 'Bodø{Enter}')

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByText('At most 20 places.')).toBeInTheDocument()
  })

  it('should accept a pasted place of exactly 80 characters', async () => {
    const { onPatch, user } = renderSearch()
    await user.click(screen.getByRole('textbox', { name: 'Places' }))

    await user.paste(`${'x'.repeat(80)},`)

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { places: ['Bergen', 'Oslo', 'x'.repeat(80)] } })
  })

  it('should not mention the place limit under it', () => {
    renderSearch()

    expect(screen.queryByText(/^At most/)).not.toBeInTheDocument()
  })

  it('should refuse a pasted place over 80 characters', async () => {
    const { onPatch, user } = renderSearch()
    await user.click(screen.getByRole('textbox', { name: 'Places' }))

    await user.paste(`${'x'.repeat(81)},`)

    expect(onPatch).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('A place has at most 80 characters.')
  })

  it('should tick and lock Open to moving when no places are set', () => {
    renderSearch(withTargets({ places: [] }))

    expect(screen.getByRole('checkbox', { name: 'Open to moving' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Open to moving' })).toBeDisabled()
  })

  it('should send Open to moving when places are set', async () => {
    const { onPatch, user } = renderSearch()

    await user.click(screen.getByRole('checkbox', { name: 'Open to moving' }))

    expect(onPatch).toHaveBeenCalledExactlyOnceWith({ targets: { relocate: true } })
  })
})
