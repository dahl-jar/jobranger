import styles from '@/features/jobs/AdRow.module.css'
import type { Mark } from '@/features/relay/contract/payload.gen'
import { Icon, type IconName } from '@/shared/ui/Icon'

const ACTION_ICON_SIZE = 16

interface MarkAction {
  mark: Mark
  label: string
  icon: IconName
  className: string | undefined
}

const MARK_ACTIONS: readonly MarkAction[] = [
  { mark: 'applied', label: 'I applied', icon: 'applied', className: styles.applied },
  { mark: 'skipped', label: 'Skip', icon: 'skip', className: styles.skip },
  { mark: 'not_for_me', label: 'Not for me', icon: 'notForMe', className: styles.notForMe },
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
          {action.label}
        </button>
      ))}
      {mark !== null && (
        <button type="button" className={styles.undo} aria-describedby={describedBy} disabled={disabled} onClick={() => onUnmark(adId)}>
          Undo
        </button>
      )}
    </>
  )
}
