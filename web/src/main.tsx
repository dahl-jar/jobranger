import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import '@/app/styles/fonts.css'
import '@/app/styles/tokens.css'
import '@/app/styles/base.css'
import { routes } from '@/app/router'
import { reloadOnce } from '@/app/routeImport'
import { forgetScroll } from '@/app/scrollPositions'
import { ThemeProvider } from '@/app/theme/ThemeProvider'
import { LanguageProvider } from '@/shared/i18n/LanguageProvider'

const container = document.getElementById('root')
if (container === null) throw new Error('index.html is missing the #root element')

const [pageLoad] = performance.getEntriesByType('navigation')
forgetScroll(window.location, pageLoad instanceof PerformanceNavigationTiming ? pageLoad.type : undefined)

const router = createBrowserRouter(routes)

window.addEventListener('vite:preloadError', (event) => {
  const { navigation, location } = router.state
  const target = navigation.location ?? location
  reloadOnce(router.createHref(target), Reflect.get(event, 'payload'), target.key)
})

createRoot(container).render(
  <StrictMode>
    <LanguageProvider>
      <ThemeProvider>
        <RouterProvider router={router} />
      </ThemeProvider>
    </LanguageProvider>
  </StrictMode>,
)
