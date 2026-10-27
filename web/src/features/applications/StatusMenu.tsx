import { useEffect, useId, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react'
import { APPLICATION_STATUSES } from '@/features/applications/applicationStatus'
import { nextMenuItem } from '@/features/applications/menuKeys'
import styles from '@/features/applications/StatusMenu.module.css'
import type { ApplicationStatus } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { Icon, type IconName } from '@/shared/ui/Icon'

const STATUS_ICONS: Record<ApplicationStatus, IconName> = {
  waiting: 'waiting',
  interview: 'interview',
  offer: 'offer',
  accepted: 'accepted',
  rejected: 'close',
}
const CHEVRON_SIZE = 12

interface StatusMenuProps {
  status: ApplicationStatus
  onChange: (status: ApplicationStatus) => void
  disabled: boolean
}

function keepClickInside(event: MouseEvent) {
  event.stopPropagation()
}

export function StatusMenu({ status, onChange, disabled }: StatusMenuProps) {
  const [open, setOpen] = useState(false)
  const menuId = useId()
  const wrapperRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([])
  const checkedIndex = APPLICATION_STATUSES.indexOf(status)
  const words = useText().applications
  const label = words.statuses[status]

  useEffect(() => {
    if (!open) return undefined
    itemRefs.current[checkedIndex]?.focus()
    function onPointerDown(event: PointerEvent) {
      if (event.target instanceof Node && wrapperRef.current?.contains(event.target)) return
      setOpen(false)
    }
    document.addEventListener('pointerdown', onPointerDown)
    return () => document.removeEventListener('pointerdown', onPointerDown)
  }, [open, checkedIndex])

  function close() {
    setOpen(false)
    buttonRef.current?.focus()
  }

  function choose(next: ApplicationStatus) {
    close()
    if (next !== status) onChange(next)
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.preventDefault()
      event.stopPropagation()
      close()
      return
    }
    if (event.key === 'Tab') {
      close()
      return
    }
    const current = itemRefs.current.findIndex((item) => item === document.activeElement)
    const next = nextMenuItem(event.key, current, APPLICATION_STATUSES.length)
    if (next === undefined) return
    event.preventDefault()
    itemRefs.current[next]?.focus()
  }

  return (
    <div ref={wrapperRef} className={styles.wrap} onClick={keepClickInside}>
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={words.statusLabel(label)}
        disabled={disabled}
        className={[styles.button, styles[status], open && styles.open].filter(Boolean).join(' ')}
        onClick={() => setOpen(!open)}
      >
        <Icon name={STATUS_ICONS[status]} />
        <span className={styles.label}>{label}</span>
        <Icon name="chevron" size={CHEVRON_SIZE} />
      </button>
      {open && (
        <div id={menuId} role="menu" aria-label={words.statusMenu} className={styles.menu} onKeyDown={onKeyDown}>
          {APPLICATION_STATUSES.map((option, index) => (
            <button
              key={option}
              ref={(node) => {
                itemRefs.current[index] = node
              }}
              type="button"
              role="menuitemradio"
              aria-checked={option === status}
              tabIndex={-1}
              className={`${styles.item} ${styles[`${option}Tone`]}`}
              onClick={() => choose(option)}
            >
              <span className={styles.itemIcon}>
                <Icon name={STATUS_ICONS[option]} />
              </span>
              <span className={styles.itemLabel}>{words.statuses[option]}</span>
              <span aria-hidden="true" className={styles.radio} />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
