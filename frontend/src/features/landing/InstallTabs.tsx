import { useEffect, useId, useState, type KeyboardEvent } from 'react'
import styles from '@/features/landing/LandingPage.module.css'
import { INSTALL_MANAGERS } from '@/features/landing/landingContent'
import { nextTab } from '@/features/landing/nextTab'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

const COPY_RESET_MS = 2000
const COPY_ICON_SIZE = 13

type CopyStatus = 'idle' | 'copied' | 'failed'

function CopyButton({ text }: { text: string }) {
  const [status, setStatus] = useState<CopyStatus>('idle')
  const labels = useText().landing.copy

  useEffect(() => {
    if (status === 'idle') return undefined
    const timer = setTimeout(() => setStatus('idle'), COPY_RESET_MS)
    return () => clearTimeout(timer)
  }, [status])

  async function copy() {
    try {
      await navigator.clipboard.writeText(text)
      setStatus('copied')
    } catch {
      setStatus('failed')
    }
  }

  return (
    <>
      <button type="button" className={`${styles.copy} ${styles[status] ?? ''}`} onClick={() => void copy()}>
        <Icon name={status === 'copied' ? 'check' : 'copy'} size={COPY_ICON_SIZE} />
        {labels[status]}
      </button>
      <span role="status" className="visually-hidden">
        {status === 'idle' ? '' : labels[status]}
      </span>
    </>
  )
}

export function InstallTabs() {
  const idPrefix = useId()
  const [selected, setSelected] = useState(0)
  const words = useText().landing
  const tabId = (index: number) => `${idPrefix}-tab-${index}`
  const panelId = (index: number) => `${idPrefix}-panel-${index}`

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const next = nextTab(event.key, selected, INSTALL_MANAGERS.length)
    if (next === undefined) return
    event.preventDefault()
    setSelected(next)
    document.getElementById(tabId(next))?.focus()
  }

  return (
    <>
      <div role="tablist" aria-label={words.packageManager} className={styles.tabs} onKeyDown={onKeyDown}>
        {INSTALL_MANAGERS.map((manager, index) => (
          <button
            key={manager.id}
            id={tabId(index)}
            type="button"
            role="tab"
            aria-selected={index === selected}
            aria-controls={panelId(index)}
            tabIndex={index === selected ? 0 : -1}
            className={styles.tab}
            onClick={() => setSelected(index)}
          >
            {manager.id}
          </button>
        ))}
      </div>
      {INSTALL_MANAGERS.map((manager, index) => (
        <div key={manager.id} id={panelId(index)} role="tabpanel" aria-labelledby={tabId(index)} hidden={index !== selected}>
          <div className={styles.commandBox}>
            <code className={styles.command}>
              <span className={styles.prompt}>$ </span>
              {manager.command}
            </code>
            <CopyButton text={manager.command} />
          </div>
          <p className={styles.note}>{words.managerNotes[manager.id]}</p>
        </div>
      ))}
    </>
  )
}
