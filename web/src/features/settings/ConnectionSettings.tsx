import { useRef, useState } from 'react'
import { flushSync } from 'react-dom'
import type { Status } from '@/features/relay/contract/payload.gen'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { InlineConfirm } from '@/features/settings/InlineConfirm'
import { dayInSentence } from '@/features/settings/dayInSentence'
import styles from '@/features/settings/Settings.module.css'
import { formatClock } from '@/shared/format/dates'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'
import { Icon } from '@/shared/ui/Icon'

const MACHINE_ICON_SIZE = 20

const FACT_SEPARATOR = ' · '

function machineFacts(status: Status, today: string, text: Messages): string {
  const words = text.settings.connection
  const lastRun = status.last_run === null ? [] : [words.lastRun(formatClock(status.last_run.finished_at))]
  return [words.version(status.cli_version), words.paired(dayInSentence(status.paired_at, today, text)), ...lastRun].join(FACT_SEPARATOR)
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
  const text = useText()
  const words = text.settings.connection

  function cancel() {
    flushSync(() => setConfirming(false))
    unpairRef.current?.focus()
  }

  return (
    <SettingsGroup title={words.machine}>
      <div className={styles.toggleRow}>
        <div className={styles.actions}>
          <Icon name="monitor" size={MACHINE_ICON_SIZE} />
          <div>
            <div className={styles.strong}>{status.machine}</div>
            <div className={isOnline ? styles.online : styles.offline}>
              <Icon name={isOnline ? 'check' : 'close'} />
              {isOnline ? words.online : words.offline}
            </div>
            <div className={styles.grow}>{machineFacts(status, today, text)}</div>
          </div>
        </div>
        {!isConfirming && (
          <Button ref={unpairRef} variant="quiet" size={40} className={styles.danger} onClick={() => setConfirming(true)}>
            {words.unpair}
          </Button>
        )}
      </div>
      {isConfirming && (
        <InlineConfirm question={words.confirm} confirmLabel={words.confirmYes} isDanger onConfirm={onUnpair} onCancel={cancel} />
      )}
      {!isPersisted && <p className={styles.note}>{words.notPersisted}</p>}
      <p className={styles.note}>{words.dataStays}</p>
    </SettingsGroup>
  )
}
