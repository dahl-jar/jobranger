import styles from '@/features/jobs/AdRow.module.css'
import type { Mark } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { Icon, type IconName } from '@/shared/ui/Icon'

const ACTION_ICON_SIZE = 16

interface MarkAction {
  mark: Mark
  icon: IconName
  className: string | undefined
}

const MARK_ACTIONS: readonly MarkAction[] = [
  { mark: 'applied', icon: 'applied', className: styles.applied },
  { mark: 'skipped', icon: 'skip', className: styles.skip },
  { mark: 'not_for_me', icon: 'notForMe', className: styles.notForMe },
]

export interface AdCallbacks {
  onMark: (adId: string, mark: Mark) => void
  onUnmark: (adId: string) => void
}

interface AdActionsProps extends AdCallbacks {
  adId: string
  mark: Mark | null
  disabled: boolean
  describedBy: string
}

export function AdActions({ adId, mark, disabled, describedBy, onMark, onUnmark }: AdActionsProps) {
  const text = useText()

  return (
    <>
      {MARK_ACTIONS.map((action) => (
        <button
          key={action.mark}
          type="button"
          className={`${styles.action} ${action.className ?? ''}`}
          aria-describedby={describedBy}
          disabled={disabled}
          onClick={() => onMark(adId, action.mark)}
        >
          <Icon name={action.icon} size={ACTION_ICON_SIZE} />
          {text.jobs.markActions[action.mark]}
        </button>
      ))}
      {mark !== null && (
        <button type="button" className={styles.undo} aria-describedby={describedBy} disabled={disabled} onClick={() => onUnmark(adId)}>
          {text.common.undo}
        </button>
      )}
    </>
  )
}
