import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import '@/app/styles/fonts.css'
import '@/app/styles/tokens.css'
import '@/app/styles/base.css'
import { routes } from '@/app/router'
import { ThemeProvider } from '@/app/theme/ThemeProvider'

const container = document.getElementById('root')
if (container === null) throw new Error('index.html is missing the #root element')

const router = createBrowserRouter(routes)

createRoot(container).render(
  <StrictMode>
    <ThemeProvider>
      <RouterProvider router={router} />
    </ThemeProvider>
  </StrictMode>,
)
