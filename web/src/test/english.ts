import { expect } from 'vitest'
import { en } from '@/shared/i18n/en'
import { nb } from '@/shared/i18n/nb'

const MARKER = '\u0001'
const SAMPLE_ARGUMENT = 2
const MIN_PHRASE_LETTERS = 3
const SCANNED_ATTRIBUTES = ['aria-label', 'placeholder', 'title', 'alt'] as const
const LETTER = /\p{L}/gu
const ARGUMENT_PATTERN = '[^\\n]{1,40}?'
const ADDRESS = /\S+@\S+|https?:\/\/\S+/g

type Shape = string | Shape[] | { [key: string]: Shape }

function isCallable(value: unknown): value is (...args: unknown[]) => unknown {
  return typeof value === 'function'
}

function callWith(value: (...args: unknown[]) => unknown, argument: unknown): string {
  return String(value(...Array.from({ length: value.length }, () => argument)))
}

export function tableShape(table: unknown): Shape {
  if (typeof table === 'string') return 'text'
  if (isCallable(table)) return 'function'
  if (Array.isArray(table)) return table.map(tableShape)
  if (typeof table === 'object' && table !== null) {
    return Object.fromEntries(Object.entries(table).map(([key, value]) => [key, tableShape(value)]))
  }
  return typeof table
}

function collect<T>(table: unknown, read: (value: unknown) => T[]): T[] {
  if (Array.isArray(table)) return table.flatMap((value) => collect(value, read))
  if (typeof table === 'object' && table !== null) return Object.values(table).flatMap((value) => collect(value, read))
  return read(table)
}

export function tableTexts(table: unknown): string[] {
  return collect(table, (value) => {
    if (typeof value === 'string') return [value]
    return isCallable(value) ? [callWith(value, SAMPLE_ARGUMENT)] : []
  })
}

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function phrasePattern(value: unknown): string[] {
  const parts = isCallable(value) ? callWith(value, MARKER).split(MARKER) : typeof value === 'string' ? [value.trim()] : []
  const letters = parts.join('').match(LETTER)?.length ?? 0
  if (letters < MIN_PHRASE_LETTERS) return []
  return [parts.map(escapeRegExp).join(ARGUMENT_PATTERN)]
}

function englishOnlyPatterns(): RegExp[] {
  const norwegian = new Set(collect(nb, phrasePattern))
  return [...new Set(collect(en, phrasePattern))]
    .filter((pattern) => !norwegian.has(pattern))
    .map((pattern) => new RegExp(`(?<!\\p{L})${pattern}(?!\\p{L})`, 'u'))
}

function ownText(element: Element): string {
  return [...element.childNodes].map((node) => (node.nodeType === Node.TEXT_NODE ? node.textContent : '')).join('')
}

function withoutData(text: string, data: readonly string[]): string {
  return data
    .filter((value) => value !== '')
    .toSorted((first, second) => second.length - first.length)
    .reduce((remaining, value) => remaining.replaceAll(value, '\n'), text)
}

function pageTexts(root: Element): string {
  const elements = [root, ...root.querySelectorAll('*')]
  const texts = elements.flatMap((element) => [ownText(element), element.textContent])
  const attributes = elements.flatMap((element) => SCANNED_ATTRIBUTES.map((name) => element.getAttribute(name) ?? ''))
  return [...texts, document.title, ...attributes].join('\n').replace(ADDRESS, '')
}

export function englishLeft(data: readonly string[] = []): string[] {
  const text = withoutData(pageTexts(document.body), data)
  return englishOnlyPatterns()
    .map((pattern) => text.match(pattern)?.[0])
    .filter((found) => found !== undefined)
}

export function expectNoEnglish(data: readonly string[] = []): void {
  expect(englishLeft(data)).toEqual([])
}
