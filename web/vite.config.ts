import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { configDefaults, defineConfig } from 'vitest/config'
import { findContracts } from './build/findContracts.ts'
import { themeInitPlugin } from './build/themeInitPlugin.ts'

const THEME_INIT_ENTRY = fileURLToPath(new URL('./src/app/theme/themeInit.ts', import.meta.url))

export default defineConfig({
  plugins: [react(), themeInitPlugin(THEME_INIT_ENTRY)],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@contracts': findContracts(fileURLToPath(new URL('.', import.meta.url))),
    },
  },
  test: {
    exclude: [...configDefaults.exclude, '.stryker-tmp/**'],
    execArgv: ['--no-experimental-webstorage'],
    projects: [
      {
        extends: true,
        test: {
          name: 'app',
          environment: 'jsdom',
          setupFiles: ['./src/test/setup.ts'],
          include: ['src/**/*.test.{ts,tsx}', 'build/**/*.test.ts'],
        },
      },
      {
        extends: true,
        test: {
          name: 'bundle',
          environment: 'node',
          include: ['tests/**/*.test.ts'],
        },
      },
    ],
  },
})
