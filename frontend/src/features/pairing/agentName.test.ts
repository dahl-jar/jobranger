import { describe, expect, it } from 'vitest'
import { agentName } from '@/features/pairing/agentName'

describe('agentName', () => {
  it.each([
    {
      agent: 'Firefox on macOS',
      userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 15.6; rv:143.0) Gecko/20100101 Firefox/143.0',
    },
    {
      agent: 'Chrome on Windows',
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36',
    },
    {
      agent: 'Edge on Windows',
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0',
    },
    {
      agent: 'Safari on iOS',
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.6 Mobile/15E148 Safari/604.1',
    },
    {
      agent: 'Chrome on Android',
      userAgent: 'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36',
    },
    { agent: 'Firefox on Linux', userAgent: 'Mozilla/5.0 (X11; Linux x86_64; rv:143.0) Gecko/20100101 Firefox/143.0' },
    { agent: 'Firefox on iOS', userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) FxiOS/143.0 Mobile/15E148 Safari/605.1.15' },
    { agent: 'Firefox', userAgent: 'Firefox/143.0' },
    { agent: 'a browser', userAgent: '' },
  ])('should name "$agent"', ({ agent, userAgent }) => {
    expect(agentName(userAgent)).toBe(agent)
  })
})
