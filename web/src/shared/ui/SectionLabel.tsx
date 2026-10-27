import type { ReactNode } from 'react'
import styles from '@/shared/ui/SectionLabel.module.css'

type HeadingLevel = 2 | 3
type RuleTone = 'ink' | 'soft'

interface SectionLabelProps {
  children: ReactNode
  aside?: ReactNode
  id?: string | undefined
  level?: HeadingLevel
  rule?: RuleTone
}

export function SectionLabel({ children, aside, id, level = 2, rule = 'ink' }: SectionLabelProps) {
  const Heading = level === 2 ? 'h2' : 'h3'
  return (
    <>
      <div className={styles.row}>
        <Heading id={id} className={styles.label}>
          {children}
        </Heading>
        {aside}
      </div>
      <div className={rule === 'ink' ? styles.rule : `${styles.rule} ${styles.soft}`} />
    </>
  )
}
