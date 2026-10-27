import { describe, expect, it, vi } from 'vitest'
import { bundleInitScript } from './initScriptPlugin.ts'

const build = vi.hoisted(() => vi.fn<() => Promise<unknown>>())

vi.mock('vite', () => ({ build }))

describe('bundleInitScript', () => {
  it.each([
    { result: [], shape: 'an empty output list' },
    { result: {}, shape: 'a watcher' },
  ])('should name the entry when the build returns $shape', async ({ result }) => {
    build.mockResolvedValue(result)

    await expect(bundleInitScript('entry.ts', 'theme-init.js')).rejects.toThrow('theme-init build of entry.ts returned no bundle')
  })
})
