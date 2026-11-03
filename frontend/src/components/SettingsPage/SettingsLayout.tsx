import { useId, type ReactNode } from 'react'
import { Link, Navigate, NavLink, useParams } from 'react-router'
import { jobsPath, settingsPath } from '@/helpers/appPaths'
import { useAppBase } from '@/hooks/useAppBase'
import { SaveResultLine, type SaveResult } from '@/components/SettingsPage/SaveResultLine'
import { DEFAULT_SECTION, SETTINGS_SECTIONS, type SettingsSectionId } from '@/constants/settingsSections'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { useText } from '@/hooks/useLanguage'
import { Icon } from '@/components/Icon/Icon'

interface SettingsLayoutProps {
  editsDisabled: boolean
  saveResult: SaveResult | undefined
  renderSection: (section: SettingsSectionId) => ReactNode
}

export function SettingsLayout({ editsDisabled, saveResult, renderSection }: SettingsLayoutProps) {
  const titleId = useId()
  const words = useText().settings
  const { section } = useParams()
  const base = useAppBase()
  const current = SETTINGS_SECTIONS.find((candidate) => candidate.id === section)
  if (current === undefined) return <Navigate to={settingsPath(base, DEFAULT_SECTION)} replace />

  return (
    <div className={styles.layout}>
      <nav aria-label={words.nav} className={styles.nav}>
        <Link to={jobsPath(base)} className={styles.back}>
          <Icon name="back" />
          {words.back}
        </Link>
        <div className={styles.navRule} />
        {SETTINGS_SECTIONS.map((entry) => (
          <NavLink key={entry.id} to={settingsPath(base, entry.id)} className={styles.navLink ?? ''}>
            {words.sections[entry.id].label}
          </NavLink>
        ))}
      </nav>
      <section aria-labelledby={titleId} className={styles.section}>
        <div className={styles.header}>
          <h2 id={titleId} className={styles.title}>
            {words.sections[current.id].label}
          </h2>
          <SaveResultLine result={saveResult} />
        </div>
        <p className={styles.sectionNote}>{words.sections[current.id].note}</p>
        <fieldset disabled={editsDisabled && current.locksOffline} className={styles.body}>
          {renderSection(current.id)}
        </fieldset>
      </section>
    </div>
  )
}
