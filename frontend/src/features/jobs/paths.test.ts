import { describe, expect, it } from 'vitest'
import { deepLinkPath, detailPath } from '@/features/jobs/paths'

describe('detailPath', () => {
  it('should put the encoded ad id in the query', () => {
    expect(detailPath('teamtailor:org/1')).toBe('/app/ad?id=teamtailor%3Aorg%2F1')
  })
})

describe('deepLinkPath', () => {
  it('should add the action after the id', () => {
    expect(deepLinkPath({ adId: 'finn:401234567', action: 'not_for_me' })).toBe(
      '/app/ad?id=finn%3A401234567&action=not_for_me',
    )
  })
})
