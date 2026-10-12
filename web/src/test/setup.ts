import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { installMatchMedia, resetMatchMedia } from '@/test/matchMedia'

installMatchMedia()

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  resetMatchMedia()
  localStorage.clear()
  document.documentElement.removeAttribute('data-theme')
})
