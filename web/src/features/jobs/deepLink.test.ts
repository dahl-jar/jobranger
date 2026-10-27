import { describe, expect, it, vi } from 'vitest'
import {
  deepLinkOutcome,
  parseAdLink,
  parseDeepLink,
  savePendingDeepLink,
  takePendingDeepLink,
} from '@/features/jobs/deepLink'
import { ADS_SNAPSHOT, APPLIED_AD, DATED_AD } from '@/features/jobs/goldenPayloads'
import { deepLinkPath, detailPath } from '@/features/jobs/paths'

const AD_PATH = '/app/ad'
const ID_MAX_CHARS = 200
const EMAIL_PATH = deepLinkPath({ adId: DATED_AD.id, action: 'applied' })

function searchOf(path: string): string {
  return path.slice(path.indexOf('?'))
}

describe('parseDeepLink', () => {
  it.each([
    ['finn:401234567', 'applied'],
    ['teamtailor:org/1', 'not_for_me'],
    ['a b+c&d=e?f#g', 'applied'],
    ['7', 'not_for_me'],
    ['ø'.repeat(ID_MAX_CHARS), 'not_for_me'],
    ['😀'.repeat(ID_MAX_CHARS), 'applied'],
  ] as const)('should read back the link built for id %s', (adId, action) => {
    expect(parseDeepLink(AD_PATH, searchOf(deepLinkPath({ adId, action })))).toEqual({ adId, action })
  })

  it.each([
    ['an unknown action', AD_PATH, '?id=finn%3A1&action=skipped'],
    ['another path', '/app/ads/finn%3A1', '?id=finn%3A1&action=applied'],
    ['no id', AD_PATH, '?action=applied'],
    ['an empty id', AD_PATH, '?id=&action=applied'],
    ['two ids', AD_PATH, '?id=finn%3A1&id=finn%3A2&action=applied'],
    ['two actions', AD_PATH, '?id=finn%3A1&action=applied&action=not_for_me'],
    ['an id over 200 characters', AD_PATH, `?id=${'a'.repeat(ID_MAX_CHARS + 1)}&action=applied`],
    ['a newline in the id', AD_PATH, '?id=finn%3A1%0A&action=applied'],
    ['a C1 control in the id', AD_PATH, '?id=finn%C2%85&action=applied'],
  ])('should ignore a link with %s', (_, pathname, search) => {
    expect(parseDeepLink(pathname, search)).toBeUndefined()
  })
})

describe('parseAdLink', () => {
  it('should open the ad without an action from a detail link', () => {
    expect(parseAdLink(AD_PATH, searchOf(detailPath('teamtailor:org/1')))).toEqual({
      adId: 'teamtailor:org/1',
      action: undefined,
    })
  })

  it('should drop an unknown action and keep the ad', () => {
    expect(parseAdLink(AD_PATH, '?id=finn%3A1&action=delete')).toEqual({ adId: 'finn:1', action: undefined })
  })
})

describe('savePendingDeepLink', () => {
  it('should skip saving when storage is blocked', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError')
    })

    expect(() => savePendingDeepLink(EMAIL_PATH)).not.toThrow()
  })
})

describe('takePendingDeepLink', () => {
  it('should return the saved email link once', () => {
    savePendingDeepLink(EMAIL_PATH)

    expect([takePendingDeepLink(), takePendingDeepLink()]).toEqual([EMAIL_PATH, undefined])
  })

  it('should keep only the id and action of a saved link', () => {
    savePendingDeepLink(`${EMAIL_PATH}&next=%2F%2Fjobs.example.test`)

    expect(takePendingDeepLink()).toBe(EMAIL_PATH)
  })

  it.each([
    '//jobs.example.test/app/ad?id=finn%3A1&action=applied',
    '/app/ad?id=finn#top&action=applied',
    '/app/settings/search',
    '/app/ad?id=finn%3A1&action=skipped',
  ])('should drop a saved path that is not an email link: %s', (path) => {
    savePendingDeepLink(path)

    expect(takePendingDeepLink()).toBeUndefined()
  })

  it('should return nothing when storage is blocked', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError')
    })

    expect(takePendingDeepLink()).toBeUndefined()
  })
})

describe('deepLinkOutcome', () => {
  it('should ask to mark an ad without that mark', () => {
    expect(deepLinkOutcome(ADS_SNAPSHOT, { adId: DATED_AD.id, action: 'applied' })).toBe('mark')
  })

  it('should report an ad that already has the mark', () => {
    expect(deepLinkOutcome(ADS_SNAPSHOT, { adId: APPLIED_AD.id, action: 'applied' })).toBe('marked')
  })

  it('should report an ad that left the list', () => {
    expect(deepLinkOutcome(ADS_SNAPSHOT, { adId: 'finn:404', action: 'applied' })).toBe('missing')
  })
})
