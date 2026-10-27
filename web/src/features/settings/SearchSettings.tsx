import type { ReactNode } from 'react'
import type { ConfigPatch, ConfigSnapshot, Level, TargetsPatch, YearBand } from '@/features/relay/contract/payload.gen'
import { CheckRow } from '@/features/settings/CheckRow'
import { PLACE_LIMIT } from '@/features/settings/chipLimits'
import { LimitedChips } from '@/features/settings/LimitedChips'
import { timingLabel, type SearchField } from '@/features/settings/searchTiming'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import styles from '@/features/settings/Settings.module.css'

const LEVEL_OPTIONS: readonly { id: Level; label: string }[] = [
  { id: 'junior', label: 'Junior' },
  { id: 'mid', label: 'Mid-level' },
  { id: 'senior', label: 'Senior' },
  { id: 'lead', label: 'Tech lead' },
  { id: 'none', label: 'No level stated' },
]

const YEAR_OPTIONS: readonly { id: YearBand; label: string }[] = [
  { id: '0-1', label: '0–1 years' },
  { id: '1-3', label: '1–3 years' },
  { id: '3-6', label: '3–6 years' },
  { id: '6+', label: '6+ years' },
]

function toggled<T>(items: readonly T[], item: T, checked: boolean): T[] {
  return checked ? [...items, item] : items.filter((existing) => existing !== item)
}

function SearchGroup({ title, field, children }: { title: string; field: SearchField; children: ReactNode }) {
  return (
    <SettingsGroup title={title} aside={<span className={styles.timing}>{timingLabel(field)}</span>}>
      {children}
    </SettingsGroup>
  )
}

interface PickListProps<T> {
  options: readonly { id: T; label: string }[]
  picked: readonly T[]
  onChange: (picked: T[]) => void
}

function PickListWithAll<T extends string>({ options, picked, onChange }: PickListProps<T>) {
  const isAll = options.every((option) => picked.includes(option.id))
  return (
    <div className={styles.inline}>
      <CheckRow label="All" checked={isAll} onChange={(checked) => onChange(checked ? options.map((option) => option.id) : [])} />
      {options.map((option) => (
        <CheckRow
          key={option.id}
          label={option.label}
          checked={picked.includes(option.id)}
          onChange={(checked) => onChange(toggled(picked, option.id, checked))}
        />
      ))}
    </div>
  )
}

export function SearchSettings({ config, onPatch }: { config: ConfigSnapshot; onPatch: (patch: ConfigPatch) => void }) {
  const { targets } = config
  const hasPlaces = targets.places.length > 0

  function patchTargets(patch: TargetsPatch) {
    onPatch({ targets: patch })
  }

  return (
    <>
      <SearchGroup title="Roles" field="roles">
        <div className={styles.grid}>
          {config.roles.map((role) => (
            <CheckRow
              key={role.id}
              label={role.label}
              checked={targets.subfields.includes(role.id)}
              onChange={(checked) => patchTargets({ subfields: toggled(targets.subfields, role.id, checked) })}
            />
          ))}
        </div>
      </SearchGroup>
      <SearchGroup title="Level" field="levels">
        <PickListWithAll options={LEVEL_OPTIONS} picked={targets.levels} onChange={(levels) => patchTargets({ levels })} />
      </SearchGroup>
      <SearchGroup title="Experience the ad asks for" field="years">
        <PickListWithAll options={YEAR_OPTIONS} picked={targets.years} onChange={(years) => patchTargets({ years })} />
      </SearchGroup>
      <SearchGroup title="Master's degree" field="hideMasters">
        <div className={styles.stack}>
          <CheckRow
            label="Hide ads that require a master's"
            checked={targets.hide_masters}
            onChange={(checked) => patchTargets({ hide_masters: checked })}
          />
        </div>
      </SearchGroup>
      <SearchGroup title="Places" field="places">
        <div className={styles.stack}>
          <LimitedChips
            label="Places"
            items={targets.places}
            limit={PLACE_LIMIT}
            placeholder={hasPlaces ? 'Add a place' : 'Anywhere in Norway'}
            onChange={(places) => patchTargets({ places })}
          />
          <span className={styles.hint}>Press Enter to add. Empty means anywhere.</span>
          <CheckRow
            label="Open to moving"
            hint={hasPlaces ? 'Other cities show as Partial fit' : 'On, since no places are set'}
            checked={!hasPlaces || targets.relocate}
            disabled={!hasPlaces}
            onChange={(checked) => patchTargets({ relocate: checked })}
          />
        </div>
      </SearchGroup>
    </>
  )
}
