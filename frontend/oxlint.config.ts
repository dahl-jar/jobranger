import { readdirSync } from 'node:fs'
import { defineConfig, type DummyRuleMap } from 'oxlint'

const REACT_PACKAGES = ['react', 'react/*', 'react-dom', 'react-dom/*', '@tanstack/react-query']
const ROUTER_PACKAGES = ['react-router']
const COMPONENT_IMPORTS = ['@/components/**', '**/components/**']
const HOOK_IMPORTS = ['@/hooks/**', '**/hooks/**']
const PARENT_FOLDERS = ['..', './..']
const OTHER_FEATURE_FILES = ['@/components/*/[a-z]*', ...PARENT_FOLDERS.map((parent) => `${parent}/**`)]
const QUERY_CLIENT = '!@/components/App/createQueryClient'
const ALLOWED_FEATURE_FILES = [
  '!@/components/*/*.module.css',
  ...PARENT_FOLDERS.flatMap((parent) => [`!${parent}/**/*.module.css`, `!${parent}/**/[A-Z]*`]),
  QUERY_CLIENT,
]
const GENERATED_FILES = ['src/**/*.gen.*']
const PLAIN_LAYERS = ['src/helpers/**', 'src/constants/**', 'src/i18n/**', 'src/contract/**', 'src/services/**']
const FEATURE_TESTS = ['src/components/*/__tests__/**']
const PLAIN_LAYER_MESSAGE =
  'helpers, constants, i18n, contract and services import no React, hook or component: move code that needs React to src/hooks or a component folder'
const HOOK_MESSAGE = 'hooks import no component: move the shared code to src/helpers or src/constants'
const FEATURE_MESSAGE =
  "Import files outside this component folder through @/ (for example @/helpers/formatDate). Another feature's lowercase file is private: import its component file, or move the shared code to src/helpers, src/hooks or src/constants."
const FEATURES = readdirSync(new URL('./src/components', import.meta.url))

interface ImportPattern {
  group: string[]
  message: string
  caseSensitive?: boolean
  allowTypeImports?: boolean
}

function restrictImports(patterns: ImportPattern[]): DummyRuleMap {
  return { 'no-restricted-imports': ['error', { patterns }] }
}

export default defineConfig({
  plugins: ['react', 'typescript', 'oxc', 'import'],
  ignorePatterns: GENERATED_FILES,
  rules: {
    'react/rules-of-hooks': 'error',
    'react/only-export-components': ['warn', { allowConstantExport: true }],
    'import/no-cycle': 'error',
  },
  overrides: [
    {
      files: PLAIN_LAYERS,
      rules: restrictImports([
        { group: [...REACT_PACKAGES, ...COMPONENT_IMPORTS, ...HOOK_IMPORTS], message: PLAIN_LAYER_MESSAGE },
        { group: ROUTER_PACKAGES, message: PLAIN_LAYER_MESSAGE, allowTypeImports: true },
      ]),
    },
    { files: ['src/hooks/**'], rules: restrictImports([{ group: COMPONENT_IMPORTS, message: HOOK_MESSAGE }]) },
    {
      files: ['src/hooks/__tests__/**'],
      rules: restrictImports([{ group: [...COMPONENT_IMPORTS, QUERY_CLIENT], message: HOOK_MESSAGE }]),
    },
    ...FEATURES.map((feature) => ({
      files: [`src/components/${feature}/**`],
      rules: restrictImports([
        {
          group: [...OTHER_FEATURE_FILES, `!@/components/${feature}/**`, ...ALLOWED_FEATURE_FILES],
          message: FEATURE_MESSAGE,
          caseSensitive: true,
        },
      ]),
    })),
    { files: FEATURE_TESTS, rules: { 'no-restricted-imports': 'off' } },
  ],
})
