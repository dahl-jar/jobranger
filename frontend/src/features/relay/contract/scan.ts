import { MAX_INTEGER_DIGITS, MAX_JSON_DEPTH } from './limits'

interface ObjectFrame {
  keys: Set<string>
  expectKey: boolean
  key: string
}

interface ArrayFrame {
  index: number
}

type Frame = ObjectFrame | ArrayFrame

export type JsonPath = readonly (string | number)[]

interface JsonScan {
  problem: string | undefined
  floatPaths: JsonPath[]
}

interface Step {
  next: number
  problem: string | undefined
}

const NUMBER_CHAR = /[0-9eE.+-]/
const NUMBER_START = /[0-9-]/
const FRACTION_OR_EXPONENT = /[.eE]/
const LONE_SURROGATE = /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/

function stringEnd(text: string, start: number): number {
  let index = start + 1
  while (index < text.length) {
    const char = text[index]
    if (char === '\\') {
      index += 2
    } else if (char === '"') {
      return index
    } else {
      index += 1
    }
  }
  return -1
}

function numberEnd(text: string, start: number): number {
  let index = start
  while (NUMBER_CHAR.test(text.charAt(index))) {
    index += 1
  }
  return index
}

function readKey(literal: string, frame: ObjectFrame): string | undefined {
  let key: string
  try {
    key = String(JSON.parse(literal))
  } catch {
    return 'object key is not a JSON string'
  }
  if (frame.keys.has(key)) {
    return 'duplicate object key'
  }
  frame.keys.add(key)
  frame.key = key
  return undefined
}

function integerProblem(token: string): string | undefined {
  const digits = token.replace('-', '').length
  if (digits > MAX_INTEGER_DIGITS) {
    return `integer longer than ${MAX_INTEGER_DIGITS} digits`
  }
  return undefined
}

function isObjectFrame(frame: Frame | undefined): frame is ObjectFrame {
  return frame !== undefined && 'keys' in frame
}

function pathOf(frames: readonly Frame[]): JsonPath {
  return frames.map((frame) => (isObjectFrame(frame) ? frame.key : frame.index))
}

function scanString(text: string, index: number, frame: Frame | undefined): Step {
  const end = stringEnd(text, index)
  if (end < 0) {
    return { next: text.length, problem: 'unterminated string' }
  }
  if (!isObjectFrame(frame) || !frame.expectKey) {
    return { next: end + 1, problem: undefined }
  }
  frame.expectKey = false
  return { next: end + 1, problem: readKey(text.slice(index, end + 1), frame) }
}

function scanNumber(text: string, index: number, frames: readonly Frame[], floatPaths: JsonPath[]): Step {
  const end = numberEnd(text, index)
  const token = text.slice(index, end)
  if (FRACTION_OR_EXPONENT.test(token)) {
    floatPaths.push(pathOf(frames))
    return { next: end, problem: undefined }
  }
  return { next: end, problem: integerProblem(token) }
}

function nextEntry(frame: Frame): void {
  if (isObjectFrame(frame)) {
    frame.expectKey = true
  } else {
    frame.index += 1
  }
}

function scanPunctuation(char: string, frames: Frame[]): string | undefined {
  const frame = frames.at(-1)
  if (char === '{' || char === '[') {
    frames.push(char === '{' ? { keys: new Set(), expectKey: true, key: '' } : { index: 0 })
    return frames.length > MAX_JSON_DEPTH ? `nesting deeper than ${MAX_JSON_DEPTH}` : undefined
  }
  if (char === '}' || char === ']') {
    frames.pop()
  } else if (char === ',' && frame !== undefined) {
    nextEntry(frame)
  }
  return undefined
}

function scanToken(text: string, index: number, frames: Frame[], floatPaths: JsonPath[]): Step {
  const char = text.charAt(index)
  if (char === '"') {
    return scanString(text, index, frames.at(-1))
  }
  if (NUMBER_START.test(char)) {
    return scanNumber(text, index, frames, floatPaths)
  }
  return { next: index + 1, problem: scanPunctuation(char, frames) }
}

export function scanJson(text: string): JsonScan {
  const frames: Frame[] = []
  const floatPaths: JsonPath[] = []
  let index = 0
  while (index < text.length) {
    const step = scanToken(text, index, frames, floatPaths)
    if (step.problem) {
      return { problem: step.problem, floatPaths }
    }
    index = step.next
  }
  return { problem: undefined, floatPaths }
}

function scalarProblem(value: unknown): string | undefined {
  if (typeof value === 'string' && LONE_SURROGATE.test(value)) {
    return 'string holds a lone surrogate'
  }
  if (typeof value === 'number' && !Number.isFinite(value)) {
    return 'non-finite number'
  }
  return undefined
}

function keyProblem(container: object): string | undefined {
  return Object.keys(container).some((key) => LONE_SURROGATE.test(key)) ? 'key holds a lone surrogate' : undefined
}

export function structureProblem(value: unknown): string | undefined {
  const pending: unknown[] = [value]
  while (pending.length > 0) {
    const current = pending.pop()
    const isContainer = typeof current === 'object' && current !== null
    const problem = isContainer ? keyProblem(current) : scalarProblem(current)
    if (problem) {
      return problem
    }
    for (const child of isContainer ? Object.values(current) : []) {
      pending.push(child)
    }
  }
  return undefined
}
