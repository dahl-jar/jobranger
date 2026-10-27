import { APPLICATION_STATUSES } from '@/features/applications/applicationStatus'
import type { Application } from '@/features/relay/contract/payload.gen'
import { oneOf } from '@/shared/oneOf'

const SORT_KEYS = ['company', 'role', 'applied', 'status', 'note'] as const
const SORT_DIRS = ['asc', 'desc'] as const

export type SortKey = (typeof SORT_KEYS)[number]
export type SortDir = (typeof SORT_DIRS)[number]

export interface SortState {
  key: SortKey
  dir: SortDir
}

const DEFAULT_SORT: SortState = { key: 'applied', dir: 'desc' }
const SORT_SEPARATOR = '.'

const collator = new Intl.Collator('nb', { sensitivity: 'base' })

type Compare = (left: Application, right: Application) => number

const COMPARE: Record<Exclude<SortKey, 'note'>, Compare> = {
  company: (left, right) => collator.compare(left.company, right.company),
  role: (left, right) => collator.compare(left.title, right.title),
  applied: (left, right) => Date.parse(left.applied_at) - Date.parse(right.applied_at),
  status: (left, right) => APPLICATION_STATUSES.indexOf(left.status) - APPLICATION_STATUSES.indexOf(right.status),
}

function compareNotes(left: Application, right: Application, dir: SortDir): number {
  if (!left.note || !right.note) {
    return Number(!left.note) - Number(!right.note)
  }
  const order = collator.compare(left.note, right.note)
  return dir === 'asc' ? order : -order
}

export function sortApplications(applications: readonly Application[], sort: SortState): Application[] {
  if (sort.key === 'note') {
    return applications.toSorted((left, right) => compareNotes(left, right, sort.dir))
  }
  const compare = COMPARE[sort.key]
  return applications.toSorted((left, right) => (sort.dir === 'asc' ? compare(left, right) : compare(right, left)))
}

export function nextSort(current: SortState, key: SortKey): SortState {
  if (current.key === key) {
    return { key, dir: current.dir === 'asc' ? 'desc' : 'asc' }
  }
  return { key, dir: key === 'applied' ? 'desc' : 'asc' }
}

export function parseSort(param: string | null): SortState {
  const parts = (param ?? '').split(SORT_SEPARATOR)
  const key = oneOf(SORT_KEYS, parts[0])
  const dir = oneOf(SORT_DIRS, parts[1])
  if (parts.length !== 2 || !key || !dir) {
    return DEFAULT_SORT
  }
  return { key, dir }
}

export function formatSort(sort: SortState): string {
  return `${sort.key}${SORT_SEPARATOR}${sort.dir}`
}
