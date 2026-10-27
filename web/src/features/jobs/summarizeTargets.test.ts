import { describe, expect, it } from 'vitest'
import { summarizeTargets } from '@/features/jobs/summarizeTargets'
import { CONFIG_SNAPSHOT } from '@/features/jobs/goldenPayloads'
import type { Targets } from '@/features/relay/contract/payload.gen'

const TARGETS = CONFIG_SNAPSHOT.targets

describe('summarizeTargets', () => {
  it.each([
    [['junior', 'mid'], 'Junior, Mid-level'],
    [['mid', 'junior'], 'Junior, Mid-level'],
    [['junior', 'mid', 'senior', 'lead', 'none'], 'All'],
    [[], 'None picked'],
  ] as const)('should sum up levels %j as %s', (levels, text) => {
    expect(summarizeTargets({ ...TARGETS, levels: [...levels] }).level).toBe(text)
  })

  it.each([
    [['0-1', '1-3'], '0–1 years, 1–3 years'],
    [['0-1', '1-3', '3-6', '6+'], 'All'],
    [[], 'None picked'],
  ] as const)('should sum up year bands %j as %s', (years, text) => {
    expect(summarizeTargets({ ...TARGETS, years: [...years] }).years).toBe(text)
  })

  it.each([
    [true, 'Not required'],
    [false, 'Allowed'],
  ])("should describe hiding master's ads %s as %s", (hideMasters, text) => {
    expect(summarizeTargets({ ...TARGETS, hide_masters: hideMasters }).masters).toBe(text)
  })

  it.each([
    [{ places: ['Bergen', 'Oslo'], relocate: false }, 'Bergen, Oslo'],
    [{ places: ['Bergen', 'Oslo'], relocate: true }, 'Bergen, Oslo, open to moving'],
    [{ places: [], relocate: false }, 'Anywhere, open to moving'],
  ] satisfies [Pick<Targets, 'places' | 'relocate'>, string][])('should sum up places %j as %s', (places, text) => {
    expect(summarizeTargets({ ...TARGETS, ...places }).places).toBe(text)
  })
})
