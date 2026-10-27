import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { configDefaults, defineConfig } from 'vitest/config'
import { findContracts } from './build/findContracts.ts'
import { themeInitPlugin } from './build/themeInitPlugin.ts'

const THEME_INIT_ENTRY = fileURLToPath(new URL('./src/app/theme/themeInit.ts', import.meta.url))
const CONTRACTS = findContracts(fileURLToPath(new URL('.', import.meta.url)))

export default defineConfig({
  plugins: [react(), themeInitPlugin(THEME_INIT_ENTRY)],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@contracts': CONTRACTS,
    },
  },
  server: {
    fs: { allow: ['.', CONTRACTS] },
  },
  build: {
    modulePreload: false,
  },
  test: {
    exclude: [...configDefaults.exclude, '.stryker-tmp/**'],
    execArgv: ['--no-experimental-webstorage'],
    projects: [
      {
        extends: true,
        test: {
          name: 'dom',
          environment: 'jsdom',
          setupFiles: ['./src/test/setup.ts'],
          include: ['src/**/*.test.{ts,tsx}'],
        },
      },
      {
        extends: true,
        test: {
          name: 'node',
          environment: 'node',
          include: ['build/**/*.test.ts', 'tests/**/*.test.ts'],
        },
      },
    ],
  },
})
