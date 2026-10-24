import type { ErrorObject } from 'ajv'
import type { Envelope } from './envelope.gen'
import type { Payload } from './payload.gen'
import { scanJson, structureProblem } from './scan'
import { payloadSchema } from './schemas.gen'
import { type StandaloneValidator, validateEnvelope, validatePayload } from './validate.gen'

const MIN_YEAR = 1
const DATE_PARTS = /^(\d{4})-(\d{2})-(\d{2})/
const TIMESTAMP_PATTERN = new RegExp(payloadSchema.$defs.Timestamp.pattern)

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

function parseJson(text: string): unknown {
  const problem = scanJson(text)
  if (problem) {
    throw new ContractError([problem], undefined)
  }
  try {
    return JSON.parse(text)
  } catch (error: unknown) {
    throw new ContractError([`invalid JSON: ${error instanceof Error ? error.message : String(error)}`], undefined)
  }
}

function decodeWith<T>(raw: unknown, validate: StandaloneValidator<T>, tag: string): T {
  const problem = structureProblem(raw)
  if (problem) {
    throw new ContractError([problem], tagOf(raw, tag))
  }
  if (validate(raw)) {
    return raw
  }
  throw new ContractError(errorMessages(validate.errors), tagOf(raw, tag))
}

export function parsePayload(text: string): Payload {
  return decodeWith(parseJson(text), validatePayload, 'type')
}

export function parseEnvelope(text: string): Envelope {
  return decodeWith(parseJson(text), validateEnvelope, 'kind')
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
