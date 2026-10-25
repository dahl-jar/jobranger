import { describe, expect, it } from 'vitest'
import { forgetScroll } from '@/app/scrollPositions'

const SCROLL_POSITIONS_KEY = 'react-router-scroll-positions'

const SAVED = { '/#install': 2819, '/': 2000, '/setup?step=3': 400, abc123: 900 }

function savedPositions(): unknown {
  return JSON.parse(sessionStorage.getItem(SCROLL_POSITIONS_KEY) ?? 'null')
}

describe('forgetScroll', () => {
  it.each([
    { url: { pathname: '/', search: '', hash: '#install' }, forgotten: '/#install' },
    { url: { pathname: '/', search: '', hash: '' }, forgotten: '/' },
    { url: { pathname: '/setup', search: '?step=3', hash: '' }, forgotten: '/setup?step=3' },
  ])('should forget the saved position of $forgotten on a typed or followed load', ({ url, forgotten }) => {
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, JSON.stringify(SAVED))

    forgetScroll(url, 'navigate')

    expect(savedPositions()).toEqual(Object.fromEntries(Object.entries(SAVED).filter(([key]) => key !== forgotten)))
  })

  it.each(['reload', 'back_forward'])('should keep the saved position on a %s load', (navigationType) => {
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, JSON.stringify(SAVED))

    forgetScroll({ pathname: '/', search: '', hash: '#install' }, navigationType)

    expect(savedPositions()).toEqual(SAVED)
  })

  it('should forget the saved position when the browser gives no navigation type', () => {
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, JSON.stringify(SAVED))

    forgetScroll({ pathname: '/', search: '', hash: '' }, undefined)

    expect(savedPositions()).not.toHaveProperty('/')
  })

  it.each(['not json', '42'])('should leave unreadable saved positions alone (%s)', (stored) => {
    sessionStorage.setItem(SCROLL_POSITIONS_KEY, stored)

    forgetScroll({ pathname: '/', search: '', hash: '' }, 'navigate')

    expect(sessionStorage.getItem(SCROLL_POSITIONS_KEY)).toBe(stored)
  })
})
