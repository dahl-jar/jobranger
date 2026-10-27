import type { ErrorObject } from 'ajv'
import type { Envelope } from './envelope.gen'
import type { AdsSnapshot, HiddenKind, Payload } from './payload.gen'
import { type JsonPath, scanJson, structureProblem } from './scan'
import { isIntegerPath, type SchemaNode } from './schemaIntegers'
import { envelopeSchema, payloadSchema } from './schemas.gen'
import { type StandaloneValidator, validateEnvelope, validatePayload } from './validate.gen'

const MIN_YEAR = 1
const DATE_PARTS = /^(\d{4})-(\d{2})-(\d{2})/
const TIMESTAMP_PATTERN = new RegExp(payloadSchema.$defs.Timestamp.pattern)
const HIDDEN_KINDS: readonly string[] = payloadSchema.$defs.HiddenKind.enum

export class ContractError extends Error {
  readonly errors: readonly string[]
  readonly messageType: string | undefined

  constructor(errors: readonly string[], messageType: string | undefined) {
    super(`contract violation: ${errors.join('; ')}`)
    this.name = 'ContractError'
    this.errors = errors
    this.messageType = messageType
  }
}

function errorMessages(errors: ErrorObject[] | null | undefined): string[] {
  return (errors ?? []).map((error) => `${error.instancePath || '/'} ${error.message}`)
}

function tagOf(raw: unknown, tag: string): string | undefined {
  if (typeof raw !== 'object' || raw === null) {
    return undefined
  }
  const value: unknown = Reflect.get(raw, tag)
  return typeof value === 'string' ? value : undefined
}

interface ParsedJson {
  raw: unknown
  floatPaths: readonly JsonPath[]
}

function pointerOf(path: JsonPath): string {
  return path.map((segment) => `/${segment}`).join('')
}

function parseJson(text: string): ParsedJson {
  const { problem, floatPaths } = scanJson(text)
  if (problem) {
    throw new ContractError([problem], undefined)
  }
  try {
    return { raw: JSON.parse(text), floatPaths }
  } catch (error: unknown) {
    throw new ContractError([`invalid JSON: ${error instanceof Error ? error.message : String(error)}`], undefined)
  }
}

function decodeWith<T>({ raw, floatPaths }: ParsedJson, validate: StandaloneValidator<T>, schema: SchemaNode, tag: string): T {
  const problem = structureProblem(raw)
  if (problem) {
    throw new ContractError([problem], tagOf(raw, tag))
  }
  if (!validate(raw)) {
    throw new ContractError(errorMessages(validate.errors), tagOf(raw, tag))
  }
  const floatPath = floatPaths.find((path) => isIntegerPath(schema, raw, path))
  if (floatPath) {
    throw new ContractError([`${pointerOf(floatPath)} must be integer`], tagOf(raw, tag))
  }
  return raw
}

function hiddenCountProblem(snapshot: AdsSnapshot): string | undefined {
  const counts = snapshot.hidden_counts
  const kindTotal = Object.entries(counts)
    .filter(([kind]) => HIDDEN_KINDS.includes(kind))
    .reduce((total, [, count]) => total + count, 0)
  if (kindTotal !== snapshot.hidden_total) {
    return '/hidden_counts must sum to hidden_total'
  }
  const listed = new Map<HiddenKind, number>()
  for (const ad of snapshot.hidden) {
    listed.set(ad.kind, (listed.get(ad.kind) ?? 0) + 1)
  }
  const undercounted = [...listed].find(([kind, count]) => counts[kind] < count)
  return undercounted && `/hidden_counts/${undercounted[0]} must not be below the listed hidden ads`
}

export function parsePayload(text: string): Payload {
  const payload = decodeWith(parseJson(text), validatePayload, payloadSchema, 'type')
  const problem = payload.type === 'ads.snapshot' ? hiddenCountProblem(payload) : undefined
  if (problem) {
    throw new ContractError([problem], payload.type)
  }
  return payload
}

export function parseEnvelope(text: string): Envelope {
  return decodeWith(parseJson(text), validateEnvelope, envelopeSchema, 'kind')
}

function isCalendarDate(year: number, month: number, day: number): boolean {
  const calendar = new Date(0)
  calendar.setUTCFullYear(year, month - 1, day)
  return year >= MIN_YEAR && calendar.getUTCDate() === day
}

export function parseTimestamp(text: string): Date {
  const parts = DATE_PARTS.exec(text)
  if (!TIMESTAMP_PATTERN.test(text) || !parts) {
    throw new ContractError(['timestamp does not match the contract pattern'], undefined)
  }
  const parsed = new Date(text)
  if (!isCalendarDate(Number(parts[1]), Number(parts[2]), Number(parts[3])) || Number.isNaN(parsed.getTime())) {
    throw new ContractError(['timestamp is not a calendar date'], undefined)
  }
  return parsed
}
