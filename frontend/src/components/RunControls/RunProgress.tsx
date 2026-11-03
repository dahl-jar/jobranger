import styles from '@/components/RunControls/RunControls.module.css'
import { runSteps, runTitle, type StepState } from '@/helpers/runProgressView'
import type { RunId, RunProgress as RunProgressMessage } from '@/contract/payload.gen'
import { Button } from '@/components/Button/Button'
import { Icon, type IconName } from '@/components/Icon/Icon'
import { ProgressBar } from '@/components/ProgressBar/ProgressBar'
import { useText } from '@/hooks/useLanguage'

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
  const text = useText()

  return (
    <div className={styles.progress}>
      <div className={styles.head}>
        <p className={styles.live}>
          <span className={styles.title}>{runTitle(progress, text)}</span>
          <span className={styles.percent}>{text.numbers.percent(progress.percent)}</span>
        </p>
        <Button
          variant="quiet"
          size={36}
          className={styles.small}
          disabled={editsDisabled || pending}
          onClick={() => onCancel(progress.run_id)}
        >
          {text.common.cancel}
        </Button>
      </div>
      <ProgressBar label={text.run.progress} percent={progress.percent} className={styles.track} />
      <ul className={styles.steps}>
        {runSteps(progress, text).map((step) => (
          <li key={step.key} className={`${styles.step} ${styles[step.state]}`}>
            <Icon name={STEP_ICONS[step.state]} size={STEP_ICON_SIZE} />
            {step.text}
          </li>
        ))}
      </ul>
    </div>
  )
}
