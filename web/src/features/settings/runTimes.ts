const MIN_GAP_MINUTES = 120
const FIRST_RUN_TIME = '03:00'
const CLOCK_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/
const MINUTES_PER_HOUR = 60
const MINUTES_PER_DAY = 1440
const SLOT_STEP_MINUTES = 30

export const MIN_GAP_HOURS = MIN_GAP_MINUTES / MINUTES_PER_HOUR

export type RunTimesError =
  | { kind: 'format'; time: string }
  | { kind: 'duplicate'; time: string }
  | { kind: 'tooClose'; pair: readonly [string, string] }

function toMinutes(time: string): number {
  const [hours = 0, minutes = 0] = time.split(':').map(Number)
  return hours * MINUTES_PER_HOUR + minutes
}

function toClock(minutes: number): string {
  const hours = String(Math.floor(minutes / MINUTES_PER_HOUR)).padStart(2, '0')
  return `${hours}:${String(minutes % MINUTES_PER_HOUR).padStart(2, '0')}`
}

function gapMinutes(first: number, second: number): number {
  const distance = Math.abs(first - second)
  return Math.min(distance, MINUTES_PER_DAY - distance)
}

export function sortTimes(times: readonly string[]): string[] {
  return [...times].sort()
}

export function validateRunTimes(times: readonly string[]): RunTimesError | undefined {
  const malformed = times.find((time) => !CLOCK_PATTERN.test(time))
  if (malformed !== undefined) return { kind: 'format', time: malformed }
  if (times.length < 2) return undefined
  const sorted = sortTimes(times)
  for (const [index, time] of sorted.entries()) {
    const next = sorted[(index + 1) % sorted.length] ?? time
    if (next === time) return { kind: 'duplicate', time }
    if (gapMinutes(toMinutes(time), toMinutes(next)) < MIN_GAP_MINUTES) return { kind: 'tooClose', pair: [time, next] }
  }
  return undefined
}

export function nextFreeTime(times: readonly string[]): string | undefined {
  if (times.length === 0) return FIRST_RUN_TIME
  const taken = times.map(toMinutes)
  const latest = Math.max(...taken)
  for (let offset = MIN_GAP_MINUTES; offset <= MINUTES_PER_DAY - MIN_GAP_MINUTES; offset += SLOT_STEP_MINUTES) {
    const candidate = (latest + offset) % MINUTES_PER_DAY
    if (taken.every((time) => gapMinutes(candidate, time) >= MIN_GAP_MINUTES)) return toClock(candidate)
  }
  return undefined
}
