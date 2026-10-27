import type { ReactNode } from 'react'
import styles from '@/features/settings/Settings.module.css'

export function SettingsFieldset({ legend, children }: { legend: string; children: ReactNode }) {
  return (
    <fieldset className={styles.group}>
      <legend className={styles.label}>{legend}</legend>
      <div className={styles.rule} />
      {children}
    </fieldset>
  )
}
