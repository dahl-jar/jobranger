import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { build } from 'vite'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'

const WEB = fileURLToPath(new URL('..', import.meta.url))
const BUILD_TIMEOUT_MS = 120_000
const PRELOAD_LIST = /m\.f=\[([^\]]*)\]/g

let outDir = ''
let preloads: string[] = []

beforeAll(async () => {
  outDir = mkdtempSync(join(tmpdir(), 'route-preload-'))
  await build({ configFile: join(WEB, 'vite.config.ts'), root: WEB, logLevel: 'silent', build: { outDir, emptyOutDir: true } })
  const assets = join(outDir, 'assets')
  const code = readdirSync(assets)
    .filter((name) => name.endsWith('.js'))
    .map((name) => readFileSync(join(assets, name), 'utf8'))
    .join('\n')
  preloads = [...code.matchAll(PRELOAD_LIST)].flatMap((match) => (match[1] ?? '').split(',').filter(Boolean))
}, BUILD_TIMEOUT_MS)

afterAll(() => {
  rmSync(outDir, { recursive: true, force: true })
})

describe('route preload', () => {
  it('should preload the setup page styles', () => {
    expect(preloads.some((file) => file.includes('SetupPage') && file.endsWith('.css"'))).toBe(true)
  })

  it('should not preload route code', () => {
    expect(preloads.filter((file) => file.endsWith('.js"'))).toEqual([])
  })
})
