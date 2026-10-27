import { describe, expect, it } from 'vitest'
import { CONFIG_SNAPSHOT } from '@/features/jobs/goldenPayloads'
import { roleLabel } from '@/features/jobs/labels'

describe('roleLabel', () => {
  it.each([
    ['backend', 'Backend'],
    ['general_software', 'Software'],
    ['game', 'Other'],
  ] as const)('should label role %s as %s', (roleType, label) => {
    expect(roleLabel(roleType, CONFIG_SNAPSHOT.roles)).toBe(label)
  })
})
