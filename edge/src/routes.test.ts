import { describe, expect, it } from 'vitest'
import { groupFor } from './routes.ts'
import { RELAY } from './test/fakeEnv.ts'

describe('groupFor', () => {
  it.each([
    ['/pair/nonce', 'pair'],
    ['/pair', 'pair'],
    ['/session/web', 'session'],
    ['/hub', 'hub'],
    ['/hub?access_token=abc', 'hub'],
  ])('should put %s in the %s group', (path, group) => {
    expect(groupFor(`${RELAY}${path}`)).toBe(group)
  })

  it.each(['/healthz', '/pairing', '//pair/nonce'])('should leave %s outside every group', (path) => {
    expect(groupFor(`${RELAY}${path}`)).toBeUndefined()
  })

  it.each([
    ['/pair/%2e%2e/healthz', undefined],
    ['/session/../pair/claim', 'pair'],
  ])('should resolve dot segments in %s before matching', (path, group) => {
    expect(groupFor(`${RELAY}${path}`)).toBe(group)
  })
})
