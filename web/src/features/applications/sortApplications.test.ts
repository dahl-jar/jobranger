import { describe, expect, it } from 'vitest'
import { nextSort, parseSort, sortApplications, type SortKey } from '@/features/applications/sortApplications'
import { foundApplication, handApplication } from '@/features/applications/testData'

const applications = [
  foundApplication,
  handApplication,
  {
    ...foundApplication,
    id: 4,
    company: 'employer one as',
    title: 'Backend Developer',
    status: 'rejected' as const,
    note: 'Phone screen',
    applied_at: '2026-10-05T08:00:00Z',
  },
  {
    ...foundApplication,
    id: 5,
    company: 'Org Two AS',
    title: 'Analytics Engineer',
    status: 'offer' as const,
    note: '',
    applied_at: '2026-10-08T08:00:00Z',
  },
]

const idsOf = (sorted: { id: number }[]) => sorted.map((application) => application.id)

describe('sortApplications', () => {
  it('should sort by applied date newest first by default', () => {
    expect(idsOf(sortApplications(applications, parseSort(null)))).toEqual([3, 5, 4, 2])
  })

  it.each<{ key: SortKey; ids: number[] }>([
    { key: 'company', ids: [4, 3, 2, 5] },
    { key: 'role', ids: [2, 5, 4, 3] },
    { key: 'applied', ids: [2, 4, 5, 3] },
    { key: 'status', ids: [3, 2, 5, 4] },
  ])('should sort ascending by $key', ({ key, ids }) => {
    expect(idsOf(sortApplications(applications, { key, dir: 'asc' }))).toEqual(ids)
  })

  it.each([
    { dir: 'asc' as const, ids: [4, 2, 3, 5] },
    { dir: 'desc' as const, ids: [2, 4, 3, 5] },
  ])('should put empty notes last when sorting notes $dir', ({ dir, ids }) => {
    expect(idsOf(sortApplications(applications, { key: 'note', dir }))).toEqual(ids)
  })

  it('should leave the given list untouched', () => {
    const given = [...applications]

    sortApplications(given, { key: 'company', dir: 'asc' })

    expect(idsOf(given)).toEqual([3, 2, 4, 5])
  })
})

describe('sortApplications in Norwegian', () => {
  it('should put Æ, Ø and Å after Z', () => {
    const companies = ['Ærlig AS', 'Zeta AS', 'Åsen AS', 'Alpha AS', 'Øya AS'].map((company, index) => ({ ...foundApplication, id: index, company }))

    expect(sortApplications(companies, { key: 'company', dir: 'asc' }).map((application) => application.company)).toEqual([
      'Alpha AS',
      'Zeta AS',
      'Ærlig AS',
      'Øya AS',
      'Åsen AS',
    ])
  })
})

describe('sortApplications by company', () => {
  it('should keep the given order for names that differ only by case', () => {
    const companies = ['Alpha AS', 'alpha AS'].map((company, index) => ({ ...foundApplication, id: index, company }))

    expect(sortApplications(companies, { key: 'company', dir: 'asc' }).map((application) => application.company)).toEqual(['Alpha AS', 'alpha AS'])
  })
})

describe('nextSort', () => {
  it.each([
    { from: 'asc' as const, to: 'desc' },
    { from: 'desc' as const, to: 'asc' },
  ])('should flip the sorted column from $from to $to', ({ from, to }) => {
    expect(nextSort({ key: 'company', dir: from }, 'company')).toEqual({ key: 'company', dir: to })
  })

  it.each<{ key: SortKey; dir: string }>([
    { key: 'applied', dir: 'desc' },
    { key: 'company', dir: 'asc' },
  ])('should start $key at $dir', ({ key, dir }) => {
    expect(nextSort({ key: 'status', dir: 'desc' }, key)).toEqual({ key, dir })
  })
})

describe('parseSort', () => {
  it('should read the key and direction', () => {
    expect(parseSort('note.asc')).toEqual({ key: 'note', dir: 'asc' })
  })

  it.each(['title.asc', 'company.up', 'company.asc.desc'])(
    'should fall back to applied newest first for "%s"',
    (param) => {
      expect(parseSort(param)).toEqual({ key: 'applied', dir: 'desc' })
    },
  )
})
