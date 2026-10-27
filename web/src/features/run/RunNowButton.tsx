import styles from '@/features/run/run.module.css'
import { Button } from '@/shared/ui/Button'
import { Icon } from '@/shared/ui/Icon'

interface RunNowButtonProps {
  running: boolean
  editsDisabled: boolean
  pending?: boolean
  onRun: () => void
}

const PLAY_ICON_SIZE = 12

export function RunNowButton({ running, editsDisabled, pending = false, onRun }: RunNowButtonProps) {
  return (
    <Button variant="quiet" size={36} className={styles.small} disabled={running || editsDisabled || pending} onClick={onRun}>
      <Icon name="play" size={PLAY_ICON_SIZE} />
      {running ? 'Running' : 'Run now'}
    </Button>
  )
}
