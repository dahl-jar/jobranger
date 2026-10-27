import { defineConfig, devices } from '@playwright/test'

const DEFAULT_PORT = 4317
const PORT = Number(process.env.E2E_PORT ?? DEFAULT_PORT)
const BASE_URL = `http://localhost:${PORT}`
const SERVER_TIMEOUT_MS = 180_000
const IS_CI = process.env.CI !== undefined

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  outputDir: '../reports/e2e',
  fullyParallel: true,
  forbidOnly: IS_CI,
  retries: 0,
  reporter: IS_CI ? 'github' : 'list',
  use: {
    baseURL: BASE_URL,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `pnpm exec vite build && pnpm exec vite preview --port ${PORT} --strictPort`,
    cwd: '..',
    url: BASE_URL,
    reuseExistingServer: false,
    timeout: SERVER_TIMEOUT_MS,
  },
})
