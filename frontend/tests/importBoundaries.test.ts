import { spawnSync } from 'node:child_process'
import { copyFileSync, mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'

const FRONTEND = fileURLToPath(new URL('..', import.meta.url))
const OXLINT = join(FRONTEND, 'node_modules', '.bin', 'oxlint')
const CONFIG = 'oxlint.config.ts'
const LINT_TIMEOUT_MS = 30_000
const FAILS = 1
const PASSES = 0
const LANDING = 'src/components/LandingPage'
const OTHER_HELPER = "import { nextMenuItem } from '@/components/StatusMenu/nextMenuItem'"
const REACT = "import { useState } from 'react'"
const ICON = "import { Icon } from '@/components/Icon/Icon'"

interface Probe {
  name: string
  path: string
  source: string
  exitCode: number
}

const PROBES: Probe[] = [
  { name: 'should fail when a helper imports React', path: 'src/helpers/probe.ts', source: REACT, exitCode: FAILS },
  { name: 'should fail when a constant imports React', path: 'src/constants/probe.ts', source: REACT, exitCode: FAILS },
  { name: 'should fail when i18n imports a component', path: 'src/i18n/probe.ts', source: ICON, exitCode: FAILS },
  { name: 'should fail when the contract imports a component', path: 'src/contract/probe.ts', source: ICON, exitCode: FAILS },
  { name: "should fail when a feature imports another feature's helper", path: `${LANDING}/otherHelper.ts`, source: OTHER_HELPER, exitCode: FAILS },
  { name: "should allow another feature's component", path: `${LANDING}/otherComponent.ts`, source: ICON, exitCode: PASSES },
  {
    name: "should allow another feature's CSS module",
    path: `${LANDING}/otherStyles.ts`,
    source: "import styles from '@/components/UsageViewStyles/usageViews.module.css'",
    exitCode: PASSES,
  },
  { name: "should allow a feature's own helper", path: `${LANDING}/ownHelper.ts`, source: "import { nextTab } from '@/components/LandingPage/nextTab'", exitCode: PASSES },
  {
    name: "should allow a capitalised non-component file of another feature",
    path: `${LANDING}/capitalisedTypes.ts`,
    source: "import type { Foo } from '@/components/StatusMenu/Foo.types'",
    exitCode: PASSES,
  },
  { name: 'should allow a router type in a helper', path: 'src/helpers/routerType.ts', source: "import type { Location } from 'react-router'", exitCode: PASSES },
  { name: 'should fail when a helper imports a router value', path: 'src/helpers/routerValue.ts', source: "import { Link } from 'react-router'", exitCode: FAILS },
  { name: 'should fail when a helper test imports React', path: 'src/helpers/__tests__/probe.test.ts', source: REACT, exitCode: FAILS },
  { name: "should allow a feature test to import another feature's helper", path: `${LANDING}/__tests__/probe.test.ts`, source: OTHER_HELPER, exitCode: PASSES },
  {
    name: 'should allow a hook test to import the query client',
    path: 'src/hooks/__tests__/probe.test.ts',
    source: "import { createQueryClient } from '@/components/App/createQueryClient'",
    exitCode: PASSES,
  },
]

let workspace = ''

beforeAll(() => {
  workspace = mkdtempSync(join(tmpdir(), 'import-boundaries-'))
  copyFileSync(join(FRONTEND, CONFIG), join(workspace, CONFIG))
  symlinkSync(join(FRONTEND, 'node_modules'), join(workspace, 'node_modules'), 'dir')
  for (const probe of PROBES) {
    const file = join(workspace, probe.path)
    mkdirSync(dirname(file), { recursive: true })
    writeFileSync(file, `${probe.source}\n`)
  }
})

afterAll(() => {
  rmSync(workspace, { recursive: true, force: true })
})

describe('oxlint.config', () => {
  it.each(PROBES)(
    '$name',
    ({ name, path, exitCode }) => {
      const result = spawnSync(OXLINT, [path], { cwd: workspace, encoding: 'utf8' })

      expect(result.status, `${name}: expected exit ${exitCode}\n${result.stdout}${result.stderr}`).toBe(exitCode)
    },
    LINT_TIMEOUT_MS,
  )
})
