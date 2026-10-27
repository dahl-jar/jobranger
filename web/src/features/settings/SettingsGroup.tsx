import { useId, type ReactNode } from 'react'
import styles from '@/features/settings/Settings.module.css'
import { SectionLabel } from '@/shared/ui/SectionLabel'

export function SettingsGroup({ title, aside, children }: { title: string; aside?: ReactNode; children: ReactNode }) {
  const titleId = useId()
  return (
    <section aria-labelledby={titleId} className={styles.group}>
      <SectionLabel id={titleId} level={3} rule="soft" aside={aside}>
        {title}
      </SectionLabel>
      {children}
    </section>
  )
}
