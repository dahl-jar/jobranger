import styles from '@/components/RunControls/RunControls.module.css'
import { Button } from '@/components/Button/Button'
import { useText } from '@/hooks/useLanguage'
import { Icon } from '@/components/Icon/Icon'

interface RunNowButtonProps {
  running: boolean
  editsDisabled: boolean
  pending?: boolean
  onRun: () => void
}

const PLAY_ICON_SIZE = 12

export function RunNowButton({ running, editsDisabled, pending = false, onRun }: RunNowButtonProps) {
  const words = useText().run

  return (
    <Button variant="quiet" size={36} className={styles.small} disabled={running || editsDisabled || pending} onClick={onRun}>
      <Icon name="play" size={PLAY_ICON_SIZE} />
      {running ? words.running : words.runNow}
    </Button>
  )
}
