import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { routes } from '@/components/App/router'
import { renderRoutes } from '@/__tests__/renderWithProviders'
import { IMPORT_FAILURE, stubLocation } from '@/__tests__/stubBrowserApis'

vi.mock('@/components/SetupPage/SetupPage', () => {
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
