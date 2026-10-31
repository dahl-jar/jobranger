import { describe, expect, it } from 'vitest'
import { nextMenuItem } from '@/features/applications/menuKeys'

const ITEM_COUNT = 5

describe('nextMenuItem', () => {
  it.each([
    { name: 'Down arrow', key: 'ArrowDown', from: 1, to: 2 },
    { name: 'Down arrow', key: 'ArrowDown', from: 4, to: 0 },
    { name: 'Up arrow', key: 'ArrowUp', from: 1, to: 0 },
    { name: 'Up arrow', key: 'ArrowUp', from: 0, to: 4 },
    { name: 'Home', key: 'Home', from: 3, to: 0 },
    { name: 'End', key: 'End', from: 1, to: 4 },
  ])('should move from item $from to $to on $name', ({ key, from, to }) => {
    expect(nextMenuItem(key, from, ITEM_COUNT)).toBe(to)
  })

  it('should leave an unrelated key to the browser', () => {
    expect(nextMenuItem('ArrowRight', 1, ITEM_COUNT)).toBeUndefined()
  })
})
