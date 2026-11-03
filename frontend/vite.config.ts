import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { configDefaults, defineConfig } from 'vitest/config'
import { findMessages } from './build/findMessages.ts'
import { initScriptPlugin } from './build/initScriptPlugin.ts'

const THEME_INIT_ENTRY = fileURLToPath(new URL('./src/helpers/paintThemeOnLoad.ts', import.meta.url))
const LANGUAGE_INIT_ENTRY = fileURLToPath(new URL('./src/helpers/setLanguageOnLoad.ts', import.meta.url))
const THEME_INIT_FILE = 'theme-init.js'
const LANGUAGE_INIT_FILE = 'language-init.js'
const MESSAGES = findMessages(fileURLToPath(new URL('.', import.meta.url)))
const NODE_SOURCE_TESTS = ['src/services/**/*.test.ts', 'src/contract/**/*.test.ts', 'src/**/*.node.test.ts']

export default defineConfig({
  plugins: [react(), initScriptPlugin(THEME_INIT_ENTRY, THEME_INIT_FILE), initScriptPlugin(LANGUAGE_INIT_ENTRY, LANGUAGE_INIT_FILE)],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@messages': MESSAGES,
    },
  },
  server: {
    fs: { allow: ['.', MESSAGES] },
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
          setupFiles: ['./src/__tests__/setup.ts'],
          include: ['src/**/*.test.{ts,tsx}'],
          exclude: [...configDefaults.exclude, '.stryker-tmp/**', ...NODE_SOURCE_TESTS],
        },
      },
      {
        extends: true,
        test: {
          name: 'node',
          environment: 'node',
          include: ['build/**/*.test.ts', 'tests/**/*.test.ts', ...NODE_SOURCE_TESTS],
        },
      },
    ],
  },
})
