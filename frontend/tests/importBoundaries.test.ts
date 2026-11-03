import { spawnSync } from 'node:child_process'
import { copyFileSync, mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'

const FRONTEND = fileURLToPath(new URL('..', import.meta.url))
const OXLINT = join(FRONTEND, 'node_modules', '.bin', 'oxlint')
const CONFIG = 'oxlint.config.ts'
const DEFAULT_FORMAT = '--format=default'
const LINT_TIMEOUT_MS = 30_000
const FAILS = 1
const PASSES = 0
const RESTRICTED_IMPORTS = 'no-restricted-imports'
const LANDING_FOLDER = 'src/components/LandingPage'
const OTHER_HELPER_IMPORT = "import { nextMenuItem } from '@/components/StatusMenu/nextMenuItem'"
const REACT_IMPORT = "import { useState } from 'react'"
const ICON_IMPORT = "import { Icon } from '@/components/Icon/Icon'"
const RELATIVE_HELPER_PATH = `${LANDING_FOLDER}/relativeHelper.ts`
const ALIAS_HINT = 'through @/'

interface Probe {
  name: string
  path: string
  source: string
}

const FAILING_PROBES: Probe[] = [
  { name: 'should fail when a helper imports React', path: 'src/helpers/probe.ts', source: REACT_IMPORT },
  { name: 'should fail when a constant imports React', path: 'src/constants/probe.ts', source: REACT_IMPORT },
  { name: 'should fail when a service imports React', path: 'src/services/probe.ts', source: REACT_IMPORT },
  {
    name: 'should fail when a helper imports a hook',
    path: 'src/helpers/hookImport.ts',
    source: "import { useText } from '@/hooks/useLanguage'",
  },
  {
    name: 'should fail when a helper imports the query library',
    path: 'src/helpers/queryImport.ts',
    source: "import { useQuery } from '@tanstack/react-query'",
  },
  { name: 'should fail when i18n imports a component', path: 'src/i18n/probe.ts', source: ICON_IMPORT },
  { name: 'should fail when the contract imports a component', path: 'src/contract/probe.ts', source: ICON_IMPORT },
  { name: 'should fail when a hook imports a component', path: 'src/hooks/componentImport.ts', source: ICON_IMPORT },
  {
    name: 'should fail when a hook test imports a component',
    path: 'src/hooks/__tests__/componentImport.test.ts',
    source: ICON_IMPORT,
  },
  {
    name: "should fail when a feature imports another feature's helper",
    path: `${LANDING_FOLDER}/otherHelper.ts`,
    source: OTHER_HELPER_IMPORT,
  },
  {
    name: "should fail when a feature imports another feature's helper by a relative path",
    path: RELATIVE_HELPER_PATH,
    source: "import { nextMenuItem } from '../StatusMenu/nextMenuItem'",
  },
  {
    name: "should fail when a feature imports another feature's helper through the components folder",
    path: `${LANDING_FOLDER}/componentsPathHelper.ts`,
    source: "import { nextMenuItem } from '../../components/StatusMenu/nextMenuItem'",
  },
  {
    name: "should fail when a feature imports another feature's helper by a path that starts with ./",
    path: `${LANDING_FOLDER}/dotPathHelper.ts`,
    source: "import { nextMenuItem } from './../StatusMenu/nextMenuItem'",
  },
  {
    name: 'should fail when a helper imports a router value',
    path: 'src/helpers/routerValue.ts',
    source: "import { Link } from 'react-router'",
  },
  { name: 'should fail when a helper test imports React', path: 'src/helpers/__tests__/probe.test.ts', source: REACT_IMPORT },
]

const PASSING_PROBES: Probe[] = [
  { name: "should allow another feature's component", path: `${LANDING_FOLDER}/otherComponent.ts`, source: ICON_IMPORT },
  {
    name: "should allow another feature's component through the components folder",
    path: `${LANDING_FOLDER}/componentsPathComponent.ts`,
    source: "import { StatusMenu } from '../../components/StatusMenu/StatusMenu'",
  },
  {
    name: "should allow another feature's CSS module",
    path: `${LANDING_FOLDER}/otherStyles.ts`,
    source: "import styles from '@/components/UsageViewStyles/usageViews.module.css'",
  },
  {
    name: "should allow another feature's CSS module by a relative path",
    path: `${LANDING_FOLDER}/relativeStyles.ts`,
    source: "import styles from '../UsageViewStyles/usageViews.module.css'",
  },
  { name: "should allow a feature's own helper", path: `${LANDING_FOLDER}/ownHelper.ts`, source: "import { nextTab } from '@/components/LandingPage/nextTab'" },
  {
    name: 'should allow a capitalised non-component file of another feature',
    path: `${LANDING_FOLDER}/capitalisedTypes.ts`,
    source: "import type { Foo } from '@/components/StatusMenu/Foo.types'",
  },
  { name: 'should allow a router type in a helper', path: 'src/helpers/routerType.ts', source: "import type { Location } from 'react-router'" },
  { name: "should allow a feature test to import another feature's helper", path: `${LANDING_FOLDER}/__tests__/probe.test.ts`, source: OTHER_HELPER_IMPORT },
  {
    name: 'should allow a hook test to import the query client',
    path: 'src/hooks/__tests__/probe.test.ts',
    source: "import { createQueryClient } from '@/components/App/createQueryClient'",
  },
]

let workspace = ''

function lint(path: string): { status: number | null; output: string } {
  const result = spawnSync(OXLINT, [DEFAULT_FORMAT, path], { cwd: workspace, encoding: 'utf8' })
  return { status: result.status, output: `${result.stdout}${result.stderr}` }
}

beforeAll(() => {
  workspace = mkdtempSync(join(tmpdir(), 'import-boundaries-'))
  copyFileSync(join(FRONTEND, CONFIG), join(workspace, CONFIG))
  symlinkSync(join(FRONTEND, 'node_modules'), join(workspace, 'node_modules'), 'dir')
  for (const probe of [...FAILING_PROBES, ...PASSING_PROBES]) {
    const file = join(workspace, probe.path)
    mkdirSync(dirname(file), { recursive: true })
    writeFileSync(file, `${probe.source}\n`)
  }
})

afterAll(() => {
  rmSync(workspace, { recursive: true, force: true })
})

describe('oxlint.config', () => {
  for (const { name, path } of FAILING_PROBES) {
    it(
      name,
      () => {
        const { status, output } = lint(path)

        expect(status, `expected exit ${FAILS}\n${output}`).toBe(FAILS)
        expect(output, `expected rule ${RESTRICTED_IMPORTS}`).toContain(RESTRICTED_IMPORTS)
      },
      LINT_TIMEOUT_MS,
    )
  }

  for (const { name, path } of PASSING_PROBES) {
    it(
      name,
      () => {
        const { status, output } = lint(path)

        expect(status, `expected exit ${PASSES}\n${output}`).toBe(PASSES)
      },
      LINT_TIMEOUT_MS,
    )
  }

  it(
    'should suggest @/ when a feature imports by a relative path',
    () => {
      const { output } = lint(RELATIVE_HELPER_PATH)

      expect(output).toContain(ALIAS_HINT)
    },
    LINT_TIMEOUT_MS,
  )
})
