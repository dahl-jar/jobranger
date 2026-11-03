import styles from '@/components/AdMarkStyles/adMarks.module.css'
import { useText } from '@/hooks/useLanguage'
import { Icon } from '@/components/Icon/Icon'
import type { Check } from '@/contract/payload.gen'

const CHECK_ICONS = { met: 'check', partly: 'dash', missing: 'close' } as const
const VISUALLY_HIDDEN = 'visually-hidden'

export function CheckList({ checks, showWord = false }: { checks: readonly Check[]; showWord?: boolean }) {
  const words = useText().ui.checks

  return (
    <ul className={styles.checks}>
      {checks.map((check) => (
        <li key={check.text} className={styles.check}>
          <span className={styles[check.state]}>
            <Icon name={CHECK_ICONS[check.state]} />
          </span>
          {check.text} <span className={showWord ? styles.word : VISUALLY_HIDDEN}>{words[check.state]}</span>
        </li>
      ))}
    </ul>
  )
}
