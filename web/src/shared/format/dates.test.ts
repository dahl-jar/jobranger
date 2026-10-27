import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { formatClock, formatDay, formatDue } from '@/shared/format/dates'

const TODAY = '2026-10-08'
const OSLO = 'Europe/Oslo'
const NEW_YORK = 'America/New_York'

beforeAll(() => {
  vi.stubEnv('TZ', OSLO)
})

afterAll(() => {
  vi.unstubAllEnvs()
})

describe('formatDue', () => {
  it.each([
    { deadline: '2026-10-10', due: '10 Oct', note: 'in 2 days' },
    { deadline: '2026-10-09', due: '9 Oct', note: 'tomorrow' },
    { deadline: '2026-10-08', due: '8 Oct', note: 'today' },
    { deadline: '2026-10-07', due: '7 Oct', note: 'passed' },
  ])('should show $due and $note for deadline $deadline', ({ deadline, due, note }) => {
    expect(formatDue(deadline, TODAY)).toEqual({ due, note })
  })

  it('should count calendar days across the start of summer time', () => {
    expect(formatDue('2026-03-30', '2026-03-28')).toEqual({ due: '30 Mar', note: 'in 2 days' })
  })

  describe('west of UTC', () => {
    beforeAll(() => {
      vi.stubEnv('TZ', NEW_YORK)
    })

    afterAll(() => {
      vi.stubEnv('TZ', OSLO)
    })

    it('should read a plain day as a local day', () => {
      expect(formatDue('2026-10-10', TODAY)).toEqual({ due: '10 Oct', note: 'in 2 days' })
    })
  })

  it.each([null, undefined])('should ask to apply early when the deadline is %s', (deadline) => {
    expect(formatDue(deadline, TODAY)).toEqual({ due: 'No date', note: 'apply early' })
  })

  it('should accept a leap day', () => {
    expect(formatDue('2028-02-29', '2028-02-27')).toEqual({ due: '29 Feb', note: 'in 2 days' })
  })

  it.each(['October 10, 2026', '2026-02-30', '2026-02-30T10:00:00Z'])('should ask to apply early when the deadline is "%s"', (deadline) => {
    expect(formatDue(deadline, TODAY)).toEqual({ due: 'No date', note: 'apply early' })
  })

  it('should leave the note empty when today is invalid', () => {
    expect(formatDue('2026-10-10', 'garbage')).toEqual({ due: '10 Oct', note: '' })
  })
})

describe('formatDay', () => {
  it.each([
    { iso: '2026-10-08', label: 'Today' },
    { iso: '2026-10-07', label: 'Yesterday' },
    { iso: '2026-10-06', label: '6 Oct' },
    { iso: '2026-10-09', label: '9 Oct' },
  ])('should label day $iso as $label', ({ iso, label }) => {
    expect(formatDay(iso, TODAY)).toBe(label)
  })

  it('should use the local day of a timestamp', () => {
    expect(formatDay('2026-10-07T22:30:00Z', TODAY)).toBe('Today')
  })

  it('should show nothing for day "2026-02-30"', () => {
    expect(formatDay('2026-02-30', TODAY)).toBe('')
  })

  it('should show the date when today is invalid', () => {
    expect(formatDay('2026-10-07', 'garbage')).toBe('7 Oct')
  })
})

describe('formatClock', () => {
  it('should show local 24-hour time', () => {
    expect(formatClock('2026-10-08T13:05:00Z')).toBe('15:05')
  })

  it('should show nothing when the time part is invalid', () => {
    expect(formatClock('2026-10-08Tgarbage')).toBe('')
  })
})
