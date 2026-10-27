import type { ReactNode } from 'react'
import type { ConfigPatch, ConfigSnapshot, Level, TargetsPatch, YearBand } from '@/features/relay/contract/payload.gen'
import { CheckRow } from '@/features/settings/CheckRow'
import { PLACE_LIMIT } from '@/features/settings/chipLimits'
import { LimitedChips } from '@/features/settings/LimitedChips'
import { timingLabel, type SearchField } from '@/features/settings/searchTiming'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import styles from '@/features/settings/Settings.module.css'
import { LEVELS, YEAR_BANDS } from '@/shared/labels/targets'
import { useText } from '@/shared/i18n/languageContext'

function toggled<T>(items: readonly T[], item: T, checked: boolean): T[] {
  return checked ? [...items, item] : items.filter((existing) => existing !== item)
}

function SearchGroup({ title, field, children }: { title: string; field: SearchField; children: ReactNode }) {
  const text = useText()
  return (
    <SettingsGroup title={title} aside={<span className={styles.timing}>{timingLabel(field, text)}</span>}>
      {children}
    </SettingsGroup>
  )
}

interface PickListProps<T extends string> {
  options: readonly T[]
  labels: Record<T, string>
  picked: readonly T[]
  onChange: (picked: T[]) => void
}

function PickListWithAll<T extends string>({ options, labels, picked, onChange }: PickListProps<T>) {
  const all = useText().settings.search.all
  const isAll = options.every((option) => picked.includes(option))
  return (
    <div className={styles.inline}>
      <CheckRow label={all} checked={isAll} onChange={(checked) => onChange(checked ? [...options] : [])} />
      {options.map((option) => (
        <CheckRow
          key={option}
          label={labels[option]}
          checked={picked.includes(option)}
          onChange={(checked) => onChange(toggled(picked, option, checked))}
        />
      ))}
    </div>
  )
}

export function SearchSettings({ config, onPatch }: { config: ConfigSnapshot; onPatch: (patch: ConfigPatch) => void }) {
  const text = useText()
  const words = text.settings.search
  const { targets } = config
  const hasPlaces = targets.places.length > 0

  function patchTargets(patch: TargetsPatch) {
    onPatch({ targets: patch })
  }

  return (
    <>
      <SearchGroup title={words.roles} field="roles">
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
      <SearchGroup title={words.level} field="levels">
        <PickListWithAll<Level> options={LEVELS} labels={text.targets.levels} picked={targets.levels} onChange={(levels) => patchTargets({ levels })} />
      </SearchGroup>
      <SearchGroup title={words.years} field="years">
        <PickListWithAll<YearBand> options={YEAR_BANDS} labels={text.targets.years} picked={targets.years} onChange={(years) => patchTargets({ years })} />
      </SearchGroup>
      <SearchGroup title={words.masters} field="hideMasters">
        <div className={styles.stack}>
          <CheckRow
            label={words.hideMasters}
            checked={targets.hide_masters}
            onChange={(checked) => patchTargets({ hide_masters: checked })}
          />
        </div>
      </SearchGroup>
      <SearchGroup title={words.places} field="places">
        <div className={styles.stack}>
          <LimitedChips
            label={words.places}
            items={targets.places}
            limit={PLACE_LIMIT}
            placeholder={hasPlaces ? words.addPlace : words.anywhere}
            onChange={(places) => patchTargets({ places })}
          />
          <span className={styles.hint}>{words.placesHint}</span>
          <CheckRow
            label={words.moving}
            hint={hasPlaces ? words.movingWithPlaces : words.movingWithoutPlaces}
            checked={!hasPlaces || targets.relocate}
            disabled={!hasPlaces}
            onChange={(checked) => patchTargets({ relocate: checked })}
          />
        </div>
      </SearchGroup>
    </>
  )
}
