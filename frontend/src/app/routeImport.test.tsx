import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { routes } from '@/app/router'
import { renderRoutes } from '@/test/render'
import { IMPORT_FAILURE, stubLocation } from '@/test/stubs'

vi.mock('@/features/setup/SetupPage', () => {
  throw new TypeError(IMPORT_FAILURE)
})

describe('routeImport', () => {
  it('should load the setup page again when its code fails to load', async () => {
    const assign = stubLocation()

    renderRoutes(routes, '/setup?step=4')

    await screen.findByRole('heading', { level: 1, name: 'Could not load this page.' })
    await waitFor(() => expect(assign).toHaveBeenCalledWith(`${window.location.origin}/setup?step=4`))
  })
})
