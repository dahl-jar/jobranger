import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { runInNewContext } from 'node:vm'
import { describe, expect, it } from 'vitest'
import { bundleInitScript } from './initScriptPlugin.ts'

const ROOT = resolve(import.meta.dirname, '..')
const ENTRY = resolve(ROOT, 'src/shared/i18n/languageInit.ts')
const SCRIPT_FILE = 'language-init.js'

function langAfterScript(script: string, stored: string | null, browser: string): string | undefined {
  const documentElement: { lang?: string } = {}
  runInNewContext(script, {
    localStorage: { getItem: () => stored },
    navigator: { language: browser },
    document: { documentElement },
  })
  return documentElement.lang
}

describe('language-init script', () => {
  it.each([
    { stored: 'nb', browser: 'en-US', lang: 'nb' },
    { stored: null, browser: 'nn-NO', lang: 'nb' },
  ])('should mark the page $lang when $stored is saved and the browser asks for $browser', async ({ stored, browser, lang }) => {
    const script = await bundleInitScript(ENTRY, SCRIPT_FILE)

    expect(langAfterScript(script, stored, browser)).toBe(lang)
  })

  it('should load language-init.js before the app in index.html', () => {
    const html = readFileSync(resolve(ROOT, 'index.html'), 'utf8')

    expect(html.indexOf(`<script src="/${SCRIPT_FILE}"></script>`)).toBeGreaterThan(-1)
    expect(html.indexOf(`/${SCRIPT_FILE}`)).toBeLessThan(html.indexOf('/src/main.tsx'))
  })
})
