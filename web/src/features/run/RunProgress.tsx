import styles from '@/features/run/run.module.css'
import { runSteps, runTitle, type StepState } from '@/features/run/runView'
import type { RunId, RunProgress as RunProgressMessage } from '@/features/relay/contract/payload.gen'
import { Button } from '@/shared/ui/Button'
import { Icon, type IconName } from '@/shared/ui/Icon'
import { ProgressBar } from '@/shared/ui/ProgressBar'

interface RunProgressProps {
  progress: RunProgressMessage
  editsDisabled: boolean
  pending?: boolean
  onCancel: (runId: RunId) => void
}

const STEP_ICON_SIZE = 13

const STEP_ICONS: Record<StepState, IconName> = {
  done: 'check',
  active: 'running',
  pending: 'pending',
  failed: 'close',
  skipped: 'dash',
}

export function RunProgress({ progress, editsDisabled, pending = false, onCancel }: RunProgressProps) {
  return (
    <div className={styles.progress}>
      <div className={styles.head}>
        <p role="status" className={styles.live}>
          <span className={styles.title}>{runTitle(progress)}</span>
          <span className={styles.percent}>{progress.percent}%</span>
        </p>
        <Button
          variant="quiet"
          size={36}
          className={styles.small}
          disabled={editsDisabled || pending}
          onClick={() => onCancel(progress.run_id)}
        >
          Cancel
        </Button>
      </div>
      <ProgressBar label="Run progress" percent={progress.percent} className={styles.track} />
      <ul className={styles.steps}>
        {runSteps(progress).map((step) => (
          <li key={step.key} className={`${styles.step} ${styles[step.state]}`}>
            <Icon name={STEP_ICONS[step.state]} size={STEP_ICON_SIZE} />
            {step.text}
          </li>
        ))}
      </ul>
    </div>
  )
}
