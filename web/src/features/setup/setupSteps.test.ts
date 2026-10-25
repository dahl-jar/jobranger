import { describe, expect, it } from 'vitest'
import { setupSteps } from '@/features/setup/setupSteps'

const TERMINAL_COLUMNS = 72
const SITE_PAIR_URL = 'https://site-host.example/pair'

describe('setupSteps', () => {
  it('should keep every terminal line within the terminal width', () => {
    const lines = setupSteps(SITE_PAIR_URL).flatMap((step) => step.lines.map(([, text]) => text))

    expect(lines.filter((text) => text.length > TERMINAL_COLUMNS)).toEqual([])
  })

  it('should give every step terminal lines', () => {
    expect(setupSteps(SITE_PAIR_URL).filter((step) => step.lines.length === 0)).toEqual([])
  })

  it('should color the dry run matches by fit', () => {
    const dryRun = setupSteps(SITE_PAIR_URL).find((step) => step.id === 'dryRun')

    expect(dryRun?.lines.filter(([, text]) => text.includes(' fit ')).map(([kind]) => kind)).toEqual(['ok', 'ok', 'part'])
  })
})
