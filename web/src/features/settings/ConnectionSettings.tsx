import { useRef, useState } from 'react'
import { flushSync } from 'react-dom'
import type { Status } from '@/features/relay/contract/payload.gen'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { InlineConfirm } from '@/features/settings/InlineConfirm'
import { dayInSentence } from '@/features/settings/dayInSentence'
import styles from '@/features/settings/Settings.module.css'
import { formatClock } from '@/shared/format/dates'
import { Button } from '@/shared/ui/Button'
import { Icon } from '@/shared/ui/Icon'

const MACHINE_ICON_SIZE = 20

function machineFacts(status: Status, today: string): string {
  const lastRun = status.last_run === null ? [] : [`last run ${formatClock(status.last_run.finished_at)}`]
  return [`jobranger ${status.cli_version}`, `paired ${dayInSentence(status.paired_at, today)}`, ...lastRun].join(' · ')
}

interface ConnectionSettingsProps {
  status: Status
  isOnline: boolean
  isPersisted: boolean
  today: string
  onUnpair: () => void
}

export function ConnectionSettings({ status, isOnline, isPersisted, today, onUnpair }: ConnectionSettingsProps) {
  const [isConfirming, setConfirming] = useState(false)
  const unpairRef = useRef<HTMLButtonElement>(null)

  function cancel() {
    flushSync(() => setConfirming(false))
    unpairRef.current?.focus()
  }

  return (
    <SettingsGroup title="Your machine">
      <div className={styles.toggleRow}>
        <div className={styles.actions}>
          <Icon name="monitor" size={MACHINE_ICON_SIZE} />
          <div>
            <div className={styles.strong}>{status.machine}</div>
            <div className={isOnline ? styles.online : styles.offline}>
              <Icon name={isOnline ? 'check' : 'close'} />
              {isOnline ? 'Online' : 'Offline'}
            </div>
            <div className={styles.grow}>{machineFacts(status, today)}</div>
          </div>
        </div>
        {!isConfirming && (
          <Button ref={unpairRef} variant="quiet" size={40} className={styles.danger} onClick={() => setConfirming(true)}>
            Unpair
          </Button>
        )}
      </div>
      {isConfirming && (
        <InlineConfirm question="Unpair this browser?" confirmLabel="Yes, unpair" isDanger onConfirm={onUnpair} onCancel={cancel} />
      )}
      {!isPersisted && <p className={styles.note}>This browser may clear the pairing after 7 days without a visit.</p>}
      <p className={styles.note}>Your data stays on your computer.</p>
    </SettingsGroup>
  )
}
