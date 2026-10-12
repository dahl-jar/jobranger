import { resolve } from 'node:path'
import { createServer, type Rollup } from 'vite'
import { describe, expect, it, onTestFinished, vi } from 'vitest'
import { bundleThemeInit, themeInitPlugin } from './themeInitPlugin.ts'

const ROOT = resolve(import.meta.dirname, '..')
const ENTRY = resolve(ROOT, 'src/app/theme/themeInit.ts')

async function fetchFromDevServer(path: string): Promise<Response> {
  const server = await createServer({ configFile: false, root: ROOT, logLevel: 'silent', plugins: [themeInitPlugin(ENTRY)], server: { port: 0, ws: false } })
  onTestFinished(() => server.close())
  await server.listen()
  return fetch(new URL(path, server.resolvedUrls?.local[0]))
}

describe('bundleThemeInit', () => {
  it('should build a classic script that applies the stored theme', async () => {
    localStorage.setItem('jobranger.theme', 'light')
    const script = await bundleThemeInit(ENTRY)

    new Function(script)()

    expect(document.documentElement.dataset.theme).toBe('light')
  })
})

describe('themeInitPlugin', () => {
  it('should emit theme-init.js into the build', async () => {
    const plugin = themeInitPlugin(ENTRY)
    const emitFile = vi.fn<(file: Rollup.EmittedFile) => string>(() => '')

    await plugin.generateBundle.call({ emitFile })

    expect(emitFile).toHaveBeenCalledWith(expect.objectContaining({ type: 'asset', fileName: 'theme-init.js' }))
  })

  it('should serve theme-init.js as a script that paints the stored theme on the dev server', async () => {
    localStorage.setItem('jobranger.theme', 'light')
    const response = await fetchFromDevServer('/theme-init.js')

    new Function(await response.text())()

    expect(response.headers.get('content-type')).toBe('text/javascript')
    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('should serve the app page as HTML on the dev server', async () => {
    const response = await fetchFromDevServer('/')

    expect(response.headers.get('content-type')).toMatch(/^text\/html/)
  })
})
