import styles from '@/shared/ui/marks.module.css'
import { Icon } from '@/shared/ui/Icon'

type CheckState = 'met' | 'partial'

export interface Check {
  text: string
  state: CheckState
}

const CHECK_ICONS = { met: 'check', partial: 'dash' } as const

export function CheckList({ checks }: { checks: readonly Check[] }) {
  return (
    <ul className={styles.checks}>
      {checks.map((check) => (
        <li key={check.text} className={styles.check}>
          <span className={styles[check.state]}>
            <Icon name={CHECK_ICONS[check.state]} />
          </span>
          {check.text}
        </li>
      ))}
    </ul>
  )
}
