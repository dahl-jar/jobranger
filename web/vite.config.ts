import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { configDefaults, defineConfig } from 'vitest/config'
import { themeInitPlugin } from './build/themeInitPlugin.ts'

const THEME_INIT_ENTRY = fileURLToPath(new URL('./src/app/theme/themeInit.ts', import.meta.url))

export default defineConfig({
  plugins: [react(), themeInitPlugin(THEME_INIT_ENTRY)],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}', 'build/**/*.test.ts'],
    exclude: [...configDefaults.exclude, '.stryker-tmp/**'],
    execArgv: ['--no-experimental-webstorage'],
  },
})
