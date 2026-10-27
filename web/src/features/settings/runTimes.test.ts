import { describe, expect, it } from 'vitest'
import { nextFreeTime, validateRunTimes } from '@/features/settings/runTimes'
import { EVERY_TWO_HOURS } from '@/features/settings/settings.samples'

const ODD_HOURS_BUT_21 = ['01:00', '03:00', '05:00', '07:00', '09:00', '11:00', '13:00', '15:00', '17:00', '19:00', '23:00']

describe('validateRunTimes', () => {
  it.each([
    { times: ['03:00'] },
    { times: ['03:00', '05:00'] },
    { times: ['23:00', '01:00'] },
  ])('should accept $times', ({ times }) => {
    expect(validateRunTimes(times)).toBeUndefined()
  })

  it('should reject 23:30 and 00:30 as too close across midnight', () => {
    expect(validateRunTimes(['00:30', '12:00', '23:30'])).toEqual({ kind: 'tooClose', pair: ['23:30', '00:30'] })
  })

  it('should reject times less than 2 hours apart', () => {
    expect(validateRunTimes(['03:30', '05:20'])).toEqual({ kind: 'tooClose', pair: ['03:30', '05:20'] })
  })

  it('should reject the same time twice', () => {
    expect(validateRunTimes(['15:00', '03:00', '15:00'])).toEqual({ kind: 'duplicate', time: '15:00' })
  })

  it.each(['3:00', '24:00', '12:60', '', 'x03:00', '03:00x'])('should reject "%s" as a clock time', (time) => {
    expect(validateRunTimes(['08:00', time])).toEqual({ kind: 'format', time })
  })
})

describe('nextFreeTime', () => {
  it('should suggest 17:00 after 03:00 and 15:00', () => {
    expect(nextFreeTime(['03:00', '15:00'])).toBe('17:00')
  })

  it('should step past slots too close to an earlier time', () => {
    expect(nextFreeTime(['01:00', '23:00'])).toBe('03:00')
  })

  it('should find the free slot 2 hours before the latest time', () => {
    expect(nextFreeTime(ODD_HOURS_BUT_21)).toBe('21:00')
  })

  it('should suggest 03:00 when there are no run times', () => {
    expect(nextFreeTime([])).toBe('03:00')
  })

  it('should return nothing when every slot is taken', () => {
    expect(nextFreeTime(EVERY_TWO_HOURS)).toBeUndefined()
  })
})
