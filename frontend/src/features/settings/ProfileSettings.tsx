import { useId, type ReactNode } from 'react'
import type { ProfileSnapshot, ProfileUpdate, ProjectRef } from '@/features/relay/contract/payload.gen'
import { payloadSchema } from '@/features/relay/contract/schemas.gen'
import { PROJECT_LIMIT, TERM_LIMIT, type ChipLimit } from '@/features/settings/chipLimits'
import { LimitedChips } from '@/features/settings/LimitedChips'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { useDraft } from '@/features/settings/useDraft'
import styles from '@/features/settings/Settings.module.css'
import { useText } from '@/shared/i18n/languageContext'

export type ProfilePatch = Omit<ProfileUpdate, 'type' | 'v' | 'id'>

const SHORT_TEXT_MAX = payloadSchema.$defs.ShortText.maxLength

function TextRow({ label, value, onCommit }: { label: string; value: string; onCommit: (value: string) => void }) {
  const inputId = useId()
  const [draft, setDraft] = useDraft(value)

  function commit() {
    const trimmed = draft.trim()
    if (trimmed !== value) onCommit(trimmed)
  }

  return (
    <div className={styles.row}>
      <label htmlFor={inputId} className={styles.rowLabel}>
        {label}
      </label>
      <input
        id={inputId}
        type="text"
        maxLength={SHORT_TEXT_MAX}
        className={`${styles.textInput} ${styles.rowValue}`}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={commit}
      />
    </div>
  )
}

interface ChipRowProps {
  label: string
  items: readonly string[]
  limit: ChipLimit
  onChange: (items: string[]) => void
}

function ChipRow({ label, items, limit, onChange }: ChipRowProps) {
  const add = useText().settings.profile.add
  return (
    <div className={styles.chipRowWrap}>
      <LimitedChips label={label} items={items} limit={limit} placeholder={add} className={styles.chipRow} onChange={onChange} />
    </div>
  )
}

function projectEdits(projects: readonly ProjectRef[], names: readonly string[]) {
  return names.map((name) => ({ id: projects.find((project) => project.name === name)?.id ?? null, name }))
}

interface ProfileSettingsProps {
  profile: ProfileSnapshot
  cvUpload: ReactNode
  children: ReactNode
  onUpdate: (patch: ProfilePatch) => void
}

export function ProfileSettings({ profile, cvUpload, children, onUpdate }: ProfileSettingsProps) {
  const words = useText().settings.profile

  return (
    <>
      {cvUpload}
      <SettingsGroup title={words.heading}>
        <TextRow label={words.experience} value={profile.summary} onCommit={(summary) => onUpdate({ summary })} />
        <TextRow label={words.education} value={profile.education} onCommit={(education) => onUpdate({ education })} />
        <ChipRow label={words.languages} items={profile.languages} limit={TERM_LIMIT} onChange={(languages) => onUpdate({ languages })} />
        <ChipRow label={words.stack} items={profile.stack} limit={TERM_LIMIT} onChange={(stack) => onUpdate({ stack })} />
        <ChipRow
          label={words.projects}
          limit={PROJECT_LIMIT}
          items={profile.projects.map((project) => project.name)}
          onChange={(names) => onUpdate({ projects: projectEdits(profile.projects, names) })}
        />
        <p className={styles.note}>{words.note}</p>
      </SettingsGroup>
      {children}
    </>
  )
}
