import { describe, expect, it } from 'vitest'
import { FROM_LIST_STATE, openedFromList } from '@/features/applications/applicationsPath'

describe('openedFromList', () => {
  it.each([
    { state: FROM_LIST_STATE, expected: true },
    { state: 'fromList', expected: false },
  ])('should return $expected for state $state', ({ state, expected }) => {
    expect(openedFromList(state)).toBe(expected)
  })
})
