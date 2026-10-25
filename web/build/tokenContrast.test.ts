import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const TOKENS = readFileSync(resolve(import.meta.dirname, '../src/app/styles/tokens.css'), 'utf8')
const AA_TEXT_CONTRAST = 4.5
const TEXT_TOKENS = ['ink', 'ink2', 'muted', 'faint']

function themeBlock(theme: 'light' | 'dark'): string {
  const start = TOKENS.indexOf(`:root[data-theme='${theme}'] {`)
  return TOKENS.slice(start, TOKENS.indexOf('}', start))
}

function token(block: string, name: string): string {
  const match = new RegExp(`--${name}: #([0-9a-f]{6});`).exec(block)
  if (match?.[1] === undefined) throw new Error(`tokens.css has no --${name} hex color in this theme`)
  return match[1]
}

function luminance(hex: string): number {
  const [red = 0, green = 0, blue = 0] = [0, 2, 4].map((offset) => {
    const channel = Number.parseInt(hex.slice(offset, offset + 2), 16) / 255
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

function contrast(first: string, second: string): number {
  const [light, dark] = [luminance(first), luminance(second)].sort((a, b) => b - a)
  return ((light ?? 0) + 0.05) / ((dark ?? 0) + 0.05)
}

describe('tokens', () => {
  it.each((['light', 'dark'] as const).flatMap((theme) => TEXT_TOKENS.map((name) => [name, theme] as const)))(
    'should give --%s at least 4.5:1 on --bg in the %s theme',
    (name, theme) => {
      const block = themeBlock(theme)

      expect(contrast(token(block, name), token(block, 'bg'))).toBeGreaterThanOrEqual(AA_TEXT_CONTRAST)
    },
  )
})
