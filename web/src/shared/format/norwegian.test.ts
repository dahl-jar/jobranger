import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { formatDay, formatDayInSentence, formatDue } from '@/shared/format/dates'
import { formatUsd } from '@/shared/format/money'
import { formatCount } from '@/shared/format/numbers'
import { formatTokens } from '@/shared/format/tokens'
import { en } from '@/shared/i18n/en'
import { nb } from '@/shared/i18n/nb'

const TODAY = '2026-10-08'
const NO_BREAK_SPACE = '\u00a0'

beforeAll(() => {
  vi.stubEnv('TZ', 'Europe/Oslo')
})

afterAll(() => {
  vi.unstubAllEnvs()
})

describe('formatDue in bokmål', () => {
  it.each([
    { deadline: '2026-10-10', due: `10.${NO_BREAK_SPACE}okt`, note: 'om 2 dager' },
    { deadline: '2026-10-09', due: `9.${NO_BREAK_SPACE}okt`, note: `i${NO_BREAK_SPACE}morgen` },
    { deadline: '2026-10-08', due: `8.${NO_BREAK_SPACE}okt`, note: `i${NO_BREAK_SPACE}dag` },
    { deadline: '2026-10-07', due: `7.${NO_BREAK_SPACE}okt`, note: 'utløpt' },
  ])('should show $due and $note for deadline $deadline', ({ deadline, due, note }) => {
    expect(formatDue(deadline, TODAY, nb)).toEqual({ due, note })
  })

  it('should ask to apply early when there is no deadline', () => {
    expect(formatDue(null, TODAY, nb)).toEqual({ due: 'Ingen dato', note: 'søk tidlig' })
  })
})

describe('formatDay in bokmål', () => {
  it.each([
    { iso: '2026-10-08', label: `I${NO_BREAK_SPACE}dag` },
    { iso: '2026-10-07', label: `I${NO_BREAK_SPACE}går` },
    { iso: '2026-10-06', label: `6.${NO_BREAK_SPACE}okt` },
  ])('should label day $iso as $label', ({ iso, label }) => {
    expect(formatDay(iso, TODAY, nb)).toBe(label)
  })

  it.each([
    ['2026-01-15', `15.${NO_BREAK_SPACE}jan`],
    ['2026-02-15', `15.${NO_BREAK_SPACE}feb`],
    ['2026-03-15', `15.${NO_BREAK_SPACE}mar`],
    ['2026-04-15', `15.${NO_BREAK_SPACE}apr`],
    ['2026-05-15', `15.${NO_BREAK_SPACE}mai`],
    ['2026-06-15', `15.${NO_BREAK_SPACE}jun`],
    ['2026-07-15', `15.${NO_BREAK_SPACE}jul`],
    ['2026-08-15', `15.${NO_BREAK_SPACE}aug`],
    ['2026-09-15', `15.${NO_BREAK_SPACE}sep`],
    ['2026-11-15', `15.${NO_BREAK_SPACE}nov`],
    ['2026-12-15', `15.${NO_BREAK_SPACE}des`],
  ])('should name the month of %s as %s', (iso, label) => {
    expect(formatDay(iso, TODAY, nb)).toBe(label)
  })
})

describe('formatDayInSentence', () => {
  it.each([
    { iso: '2026-10-08', day: `i${NO_BREAK_SPACE}dag` },
    { iso: '2026-10-07', day: `i${NO_BREAK_SPACE}går` },
  ])('should write $day in lowercase', ({ iso, day }) => {
    expect(formatDayInSentence(iso, TODAY, nb)).toBe(day)
  })

  it('should keep an older day as it is', () => {
    expect(formatDayInSentence('2026-10-06', TODAY, nb)).toBe(`6.${NO_BREAK_SPACE}okt`)
  })
})

describe('nb.dates', () => {
  it('should join the words of every relative day with a no-break space', () => {
    const days = [nb.dates.today, nb.dates.yesterday, nb.dates.dueToday, nb.dates.dueTomorrow]

    expect(days.filter((day) => day.includes(' '))).toEqual([])
  })
})

describe('formatTokens in bokmål', () => {
  it.each([
    { count: 999, text: '999' },
    { count: 12_400, text: '12k' },
    { count: 1_250_000, text: `1,3${NO_BREAK_SPACE}mill.` },
    { count: 2_300_000_000, text: `2,3${NO_BREAK_SPACE}mrd.` },
  ])('should show $count as $text', ({ count, text }) => {
    expect(formatTokens(count, nb)).toBe(text)
  })
})

describe('formatUsd in bokmål', () => {
  it.each([
    { value: 1.5, text: '$1,50' },
    { value: 1234.5, text: `$1${NO_BREAK_SPACE}234,50` },
    { value: 0.004, text: '<$0,01' },
    { value: Number.NaN, text: '$0,00' },
  ])('should show $value as $text', ({ value, text }) => {
    expect(formatUsd(value, nb)).toBe(text)
  })

  it('should keep the minus sign in front of the dollar sign', () => {
    expect(formatUsd(-2, en)).toBe('-$2.00')
  })
})

describe('formatCount', () => {
  it.each([
    { text: en, count: 12_345, shown: '12,345' },
    { text: nb, count: 12_345, shown: `12${NO_BREAK_SPACE}345` },
  ])('should show $shown', ({ text, count, shown }) => {
    expect(formatCount(count, text)).toBe(shown)
  })
})
