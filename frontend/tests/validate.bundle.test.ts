import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { build } from 'vite'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { findMessages } from '../build/findMessages.ts'
import { ENVELOPE_INVALID_COUNT, GOLDEN_COUNT, PAYLOAD_INVALID_COUNT } from '../src/test/fixtureCounts.ts'

interface ValidateBundle {
  parsePayload(text: string): unknown
  parseEnvelope(text: string): unknown
}

const WEB = fileURLToPath(new URL('..', import.meta.url))
const FIXTURES = join(findMessages(WEB), 'fixtures')
const ENTRY = join(WEB, 'src', 'features', 'relay', 'contract', 'validate.ts')
const RUNTIME_CODEGEN = ['new Function', 'eval(']
const BUILD_TIMEOUT_MS = 120_000
const BUNDLE_NAME = 'validate'
const CONTRACT_ERROR = 'ContractError'

const fixtureTexts = (folder: string) =>
  readdirSync(join(FIXTURES, folder))
    .filter((name) => name.endsWith('.json'))
    .sort()
    .map((name) => [name, readFileSync(join(FIXTURES, folder, name), 'utf8')] as const)

const goldens = [
  ...fixtureTexts('payload').map(([name, text]) => ['payload', name, text] as const),
  ...fixtureTexts('envelope').map(([name, text]) => ['envelope', name, text] as const),
]
const invalids = [
  ...fixtureTexts('payload-invalid').map(([name, text]) => ['payload', name, text] as const),
  ...fixtureTexts('envelope-invalid').map(([name, text]) => ['envelope', name, text] as const),
]

let outDir = ''
let code = ''
let bundle: ValidateBundle

function parse(kind: string, text: string): unknown {
  return kind === 'payload' ? bundle.parsePayload(text) : bundle.parseEnvelope(text)
}

function errorName(action: () => unknown): string {
  try {
    action()
  } catch (error: unknown) {
    return error instanceof Error ? error.name : typeof error
  }
  return 'none'
}

beforeAll(async () => {
  outDir = mkdtempSync(join(tmpdir(), 'validate-bundle-'))
  await build({
    configFile: join(WEB, 'vite.config.ts'),
    root: WEB,
    logLevel: 'silent',
    build: { outDir, emptyOutDir: true, lib: { entry: ENTRY, formats: ['es'], fileName: BUNDLE_NAME } },
  })
  const file = join(outDir, `${BUNDLE_NAME}.js`)
  code = readFileSync(file, 'utf8')
  bundle = await import(pathToFileURL(file).href)
}, BUILD_TIMEOUT_MS)

afterAll(() => {
  rmSync(outDir, { recursive: true, force: true })
})

describe('validate bundle', () => {
  it('should bundle validators without runtime code generation', () => {
    expect(RUNTIME_CODEGEN.filter((token) => code.includes(token))).toEqual([])
  })

  it('should find every fixture', () => {
    const counts = invalids.reduce(
      (total, [kind]) => ({ ...total, [kind]: (total[kind] ?? 0) + 1 }),
      {} as Record<string, number>,
    )

    expect([goldens.length, counts.payload, counts.envelope]).toEqual([
      GOLDEN_COUNT,
      PAYLOAD_INVALID_COUNT,
      ENVELOPE_INVALID_COUNT,
    ])
  })

  it.each(goldens)('should accept %s golden %s', (kind, _, text) => {
    expect(parse(kind, text)).toEqual(JSON.parse(text))
  })

  it.each(invalids)('should reject %s invalid %s with ContractError', (kind, _, text) => {
    expect(errorName(() => parse(kind, text))).toBe(CONTRACT_ERROR)
  })
})
