import { useId, useState } from 'react'
import { focusOnMount } from '@/helpers/focusOnMount'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { useText } from '@/hooks/useLanguage'
import { Button } from '@/components/Button/Button'

interface InlineConfirmProps {
  question: string
  confirmLabel: string
  isDanger?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export function InlineConfirm({ question, confirmLabel, isDanger = false, onConfirm, onCancel }: InlineConfirmProps) {
  const questionId = useId()
  const cancel = useText().common.cancel
  const [isConfirmed, setConfirmed] = useState(false)

  function confirm() {
    if (isConfirmed) return
    setConfirmed(true)
    onConfirm()
  }

  return (
    <div role="group" aria-labelledby={questionId} className={styles.confirm}>
      <span id={questionId}>{question}</span>
      <Button
        ref={focusOnMount}
        variant="quiet"
        size={36}
        className={isDanger ? styles.danger : undefined}
        aria-disabled={isConfirmed}
        onClick={confirm}
      >
        {confirmLabel}
      </Button>
      <button type="button" className={styles.textButton} onClick={() => onCancel()}>
        {cancel}
      </button>
    </div>
  )
}
