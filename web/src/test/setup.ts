import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { installMatchMedia, resetMatchMedia } from '@/test/matchMedia'

installMatchMedia()
window.scrollTo = () => undefined
Element.prototype.scrollIntoView = () => undefined

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  resetMatchMedia()
  localStorage.clear()
  sessionStorage.clear()
  document.documentElement.removeAttribute('data-theme')
  document.documentElement.removeAttribute('lang')
})
