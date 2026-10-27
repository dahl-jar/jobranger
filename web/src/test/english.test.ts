import { describe, expect, it } from 'vitest'
import { englishLeft } from '@/test/english'

function showPage(text: string) {
  document.body.innerHTML = `<main><h1>${text}</h1></main>`
}

describe('englishLeft', () => {
  it('should find English table text on the page', () => {
    showPage('No page here.')

    expect(englishLeft()).toContain('No page here.')
  })

  it('should find English table text with an argument filled in', () => {
    showPage('Setup, step 5 of 8')

    expect(englishLeft()).toContain('Setup, step 5 of 8')
  })

  it('should skip English text that comes from the data', () => {
    showPage('No page here.')

    expect(englishLeft(['No page here.'])).toEqual([])
  })

  it('should find no English on a page written in bokmål', () => {
    showPage('Status: Ingen side her.')

    expect(englishLeft()).toEqual([])
  })
})
