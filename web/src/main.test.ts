import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const SCROLL_POSITIONS_KEY = 'react-router-scroll-positions'

describe('main', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('PerformanceNavigationTiming', class {})
    document.body.innerHTML = '<div id="root"></div>'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('should show the landing page in the root element', async () => {
    await import('@/main')

    expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('should forget the saved position of the opened page on a new load', async () => {
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, JSON.stringify({ '/': 500 }))

    await import('@/main')

    expect(JSON.parse(sessionStorage.getItem(SCROLL_POSITIONS_KEY) ?? 'null')).toEqual({})
  })

  it('should name the missing root element when index.html has none', async () => {
    document.body.innerHTML = ''

    await expect(import('@/main')).rejects.toThrow('index.html is missing the #root element')
  })
})
