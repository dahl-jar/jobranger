import type { ReactNode } from 'react'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'

export function SettingsFieldset({ legend, children }: { legend: string; children: ReactNode }) {
  return (
    <fieldset className={styles.group}>
      <legend className={styles.label}>{legend}</legend>
      <div className={styles.rule} />
      {children}
    </fieldset>
  )
}
