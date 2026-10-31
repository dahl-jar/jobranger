import { describe, expect, it } from 'vitest'
import { nextTab } from '@/features/landing/nextTab'

const TAB_COUNT = 3

describe('nextTab', () => {
  it.each([
    { name: 'Right arrow', key: 'ArrowRight', start: 'last', target: 'the first tab', from: 2, to: 0 },
    { name: 'Left arrow', key: 'ArrowLeft', start: 'first', target: 'the last tab', from: 0, to: 2 },
    { name: 'Home', key: 'Home', start: 'middle', target: 'the first tab', from: 1, to: 0 },
    { name: 'End', key: 'End', start: 'middle', target: 'the last tab', from: 1, to: 2 },
    { name: 'Down arrow', key: 'ArrowDown', start: 'middle', target: 'no tab', from: 1, to: undefined },
  ])('should move from the $start tab to $target on $name', ({ key, from, to }) => {
    expect(nextTab(key, from, TAB_COUNT)).toBe(to)
  })
})
