import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import '@/styles/fonts.css'
import '@/styles/tokens.css'
import '@/styles/base.css'
import { createQueryClient } from '@/components/App/createQueryClient'
import { routes } from '@/components/App/router'
import { reloadOnce } from '@/helpers/routeImport'
import { forgetScroll } from '@/helpers/scrollPositions'
import { ThemeProvider } from '@/components/ThemeProvider/ThemeProvider'
import { LanguageProvider } from '@/components/LanguageProvider/LanguageProvider'

const container = document.getElementById('root')
if (container === null) throw new Error('index.html is missing the #root element')

const [pageLoad] = performance.getEntriesByType('navigation')
forgetScroll(window.location, pageLoad instanceof PerformanceNavigationTiming ? pageLoad.type : undefined)

const router = createBrowserRouter(routes)
const queryClient = createQueryClient()

window.addEventListener('vite:preloadError', (event) => {
  const { navigation, location } = router.state
  const target = navigation.location ?? location
  reloadOnce(router.createHref(target), Reflect.get(event, 'payload'), target.key)
})

createRoot(container).render(
  <StrictMode>
    <LanguageProvider>
      <ThemeProvider>
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </ThemeProvider>
    </LanguageProvider>
  </StrictMode>,
)
