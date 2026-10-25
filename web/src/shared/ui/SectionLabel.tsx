import type { ReactNode } from 'react'
import styles from '@/shared/ui/SectionLabel.module.css'

export function SectionLabel({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  return (
    <>
      <div className={styles.row}>
        <h2 className={styles.label}>{children}</h2>
        {aside}
      </div>
      <div className={styles.rule} />
    </>
  )
}
