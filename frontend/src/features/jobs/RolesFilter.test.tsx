import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { CONFIG_SNAPSHOT } from '@/features/jobs/goldenPayloads'
import { RolesFilter } from '@/features/jobs/RolesFilter'

function renderFilter(disabled = false) {
  const onChange = vi.fn()
  render(
    <RolesFilter
      roles={CONFIG_SNAPSHOT.roles}
      picked={CONFIG_SNAPSHOT.targets.subfields}
      disabled={disabled}
      onChange={onChange}
    />,
  )
  return onChange
}

describe('RolesFilter', () => {
  it.each([
    ['Frontend', ['backend', 'frontend', 'data_engineering']],
    ['Backend', ['data_engineering']],
  ])('should send the role list with %s toggled', async (label, next) => {
    const user = userEvent.setup()
    const onChange = renderFilter()

    await user.click(screen.getByRole('checkbox', { name: label }))

    expect(onChange).toHaveBeenCalledWith(next)
  })

  it('should tick the picked roles', () => {
    renderFilter()

    expect(screen.getByRole('checkbox', { name: 'Backend' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Frontend' })).not.toBeChecked()
  })

  it('should lock the roles when edits are off', () => {
    renderFilter(true)

    expect(screen.getByRole('checkbox', { name: 'Backend' })).toBeDisabled()
  })
})
