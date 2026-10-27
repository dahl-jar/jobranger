import { cloudflareTest } from '@cloudflare/vitest-plugin'
import { defineConfig } from 'vitest/config'

const WRANGLER_CONFIG = './wrangler.toml'

export default defineConfig({
  test: {
    projects: [
      {
        plugins: [cloudflareTest({ wrangler: { configPath: WRANGLER_CONFIG } })],
        test: {
          name: 'worker',
          include: ['src/**/*.test.ts'],
        },
      },
      {
        test: {
          name: 'config',
          environment: 'node',
          include: ['wrangler.test.ts'],
        },
      },
    ],
  },
})
