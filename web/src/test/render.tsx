import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import { LANGUAGE_STORAGE_KEY } from '@/shared/i18n/language'
import { LanguageProvider } from '@/shared/i18n/LanguageProvider'

export function renderRoutes(routes: RouteObject[], path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(
    <LanguageProvider>
      <ThemeProvider>
        <RouterProvider router={router} />
      </ThemeProvider>
    </LanguageProvider>,
  )
  return { ...view, router }
}

export function renderNorwegianRoutes(routes: RouteObject[], path: string) {
  localStorage.setItem(LANGUAGE_STORAGE_KEY, 'nb')
  return renderRoutes(routes, path)
}

const ROOT_PATH = '/'

export function renderNorwegian(element: ReactNode) {
  return renderNorwegianRoutes([{ path: '*', element }], ROOT_PATH)
}
