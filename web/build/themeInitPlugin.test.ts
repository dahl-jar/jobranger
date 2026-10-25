import { mkdtempSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { runInNewContext } from 'node:vm'
import { createServer, type Rollup } from 'vite'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { bundleThemeInit, themeInitPlugin } from './themeInitPlugin.ts'

const ROOT = resolve(import.meta.dirname, '..')
const ENTRY = resolve(ROOT, 'src/app/theme/themeInit.ts')

function paintWithStoredTheme(script: string, stored: string): string | undefined {
  const dataset: { theme?: string } = {}
  runInNewContext(script, {
    localStorage: { getItem: () => stored },
    window: { matchMedia: () => ({ matches: false }) },
    document: { documentElement: { dataset } },
  })
  return dataset.theme
}

async function fetchFromDevServer(path: string): Promise<Response> {
  const server = await createServer({ configFile: false, root: ROOT, logLevel: 'silent', plugins: [themeInitPlugin(ENTRY)], server: { port: 0, ws: false } })
  onTestFinished(() => server.close())
  await server.listen()
  return fetch(new URL(path, server.resolvedUrls?.local[0]))
}

describe('bundleThemeInit', () => {
  it('should build a classic script that applies the stored theme', async () => {
    const script = await bundleThemeInit(ENTRY)

    expect(paintWithStoredTheme(script, 'light')).toBe('light')
  })

  it('should minify the script', async () => {
    const script = await bundleThemeInit(ENTRY)

    expect(script).not.toContain('readStoredTheme')
  })

  it('should leave the disk untouched', async () => {
    const root = mkdtempSync(join(tmpdir(), 'theme-init-'))
    const cwd = vi.spyOn(process, 'cwd').mockReturnValue(root)
    onTestFinished(() => {
      cwd.mockRestore()
      rmSync(root, { recursive: true })
    })

    await bundleThemeInit(ENTRY)

    expect(readdirSync(root)).toEqual([])
  })
})

describe('themeInitPlugin', () => {
  it('should name itself jobranger-theme-init', () => {
    expect(themeInitPlugin(ENTRY).name).toBe('jobranger-theme-init')
  })

  it('should emit theme-init.js into the build', async () => {
    const plugin = themeInitPlugin(ENTRY)
    const emitFile = vi.fn<(file: Rollup.EmittedFile) => string>(() => '')

    await plugin.generateBundle.call({ emitFile })

    expect(emitFile).toHaveBeenCalledWith(expect.objectContaining({ type: 'asset', fileName: 'theme-init.js' }))
  })

  it('should serve theme-init.js as a script that paints the stored theme on the dev server', async () => {
    const response = await fetchFromDevServer('/theme-init.js')

    const theme = paintWithStoredTheme(await response.text(), 'light')

    expect(response.headers.get('content-type')).toBe('text/javascript')
    expect(theme).toBe('light')
  })

  it('should serve the app page as HTML on the dev server', async () => {
    const response = await fetchFromDevServer('/')

    expect(response.headers.get('content-type')).toMatch(/^text\/html/)
  })
})
