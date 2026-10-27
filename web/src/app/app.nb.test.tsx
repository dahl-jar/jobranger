import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PageLoading } from '@/app/PageLoading'
import { RouteError } from '@/app/RouteError'
import { routeImport } from '@/app/routeImport'
import { routes } from '@/app/router'
import { ThemeSwitch } from '@/app/theme/ThemeSwitch'
import { expectNoEnglish } from '@/test/english'
import { renderNorwegian, renderNorwegianRoutes } from '@/test/render'
import { IMPORT_FAILURE } from '@/test/stubs'

function Broken(): never {
  throw new Error('render failed')
}

describe('app shell in bokmål', () => {
  it('should show the missing page in bokmål', async () => {
    renderNorwegianRoutes(routes, '/nowhere')

    expect(await screen.findByRole('heading', { name: 'Ingen side her.' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should name the setup walkthrough in bokmål in the header', async () => {
    renderNorwegianRoutes(routes, '/setup')

    expect(await screen.findByRole('link', { name: 'Hjem' })).toBeInTheDocument()
    expect(screen.getByText('Gjennomgang av oppsettet')).toBeInTheDocument()
  })

  it('should show a broken page in bokmål', async () => {
    renderNorwegianRoutes([{ path: '/', ErrorBoundary: RouteError, Component: Broken }], '/')

    expect(await screen.findByRole('heading', { name: 'Noe gikk galt på denne siden.' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should show a page that failed to load in bokmål', async () => {
    const lazy = routeImport(() => Promise.reject(new TypeError(IMPORT_FAILURE)))
    renderNorwegianRoutes([{ ErrorBoundary: RouteError, children: [{ path: '/setup', lazy }] }], '/setup')

    expect(await screen.findByRole('heading', { name: 'Kunne ikke laste siden.' })).toBeInTheDocument()
    expectNoEnglish()
  })

  it('should show the loading line in bokmål', () => {
    renderNorwegian(<PageLoading />)

    expect(screen.getByRole('status')).toHaveTextContent('Laster ...')
  })

  it.each(['icons', 'labeled'] as const)('should label the %s theme switch in bokmål', (variant) => {
    renderNorwegian(<ThemeSwitch variant={variant} />)

    expect(screen.getByRole('group', { name: 'Tema' })).toBeInTheDocument()
    expectNoEnglish()
  })
})
