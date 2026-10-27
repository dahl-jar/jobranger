import styles from '@/shared/ui/marks.module.css'
import { Icon } from '@/shared/ui/Icon'

type CheckState = 'met' | 'partly' | 'missing'

export interface Check {
  text: string
  state: CheckState
}

const CHECK_ICONS = { met: 'check', partly: 'dash', missing: 'close' } as const
const VISUALLY_HIDDEN = 'visually-hidden'

export function CheckList({ checks, showWord = false }: { checks: readonly Check[]; showWord?: boolean }) {
  return (
    <ul className={styles.checks}>
      {checks.map((check) => (
        <li key={check.text} className={styles.check}>
          <span className={styles[check.state]}>
            <Icon name={CHECK_ICONS[check.state]} />
          </span>
          {check.text} <span className={showWord ? styles.word : VISUALLY_HIDDEN}>{check.state}</span>
        </li>
      ))}
    </ul>
  )
}
