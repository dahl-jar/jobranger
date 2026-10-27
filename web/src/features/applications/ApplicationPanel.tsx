import { useEffect, useId, useRef, useState, type MouseEvent, type PointerEvent, type SyntheticEvent } from 'react'
import { useLocation, useNavigate } from 'react-router'
import styles from '@/features/applications/ApplicationPanel.module.css'
import { APPLICATIONS_PATH, openedFromList } from '@/features/applications/applicationsPath'
import { NOTE_MAX_CHARS } from '@/features/applications/limits'
import { neighborButton, rowOrAddButton } from '@/features/applications/rowFocus'
import { StatusMenu } from '@/features/applications/StatusMenu'
import type { Application, ApplicationStatus, RoleOption, SourceId } from '@/features/relay/contract/payload.gen'
import { formatDay } from '@/shared/format/dates'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'
import { CheckList } from '@/shared/ui/CheckList'
import { FitMark } from '@/shared/ui/FitMark'
import { Icon } from '@/shared/ui/Icon'

const COMPANY_SEARCH_URL = 'https://www.google.com/search?q='

const SOURCE_NAMES: Record<SourceId, string> = {
  nav: 'NAV',
  finn: 'FINN',
  linkedin: 'LinkedIn',
  teamtailor: 'Teamtailor',
  ashby: 'Ashby',
}

const CLOSE_ICON_SIZE = 16
const LINK_ICON_SIZE = 13

interface ApplicationPanelProps {
  application: Application
  today: string
  roles: readonly RoleOption[]
  editsDisabled: boolean
  onStatusChange: (applicationId: number, status: ApplicationStatus) => void
  onNoteChange: (applicationId: number, note: string) => void
  onRemove: (application: Application) => void
}

export function ApplicationPanel(props: ApplicationPanelProps) {
  return <PanelDialog key={props.application.id} {...props} />
}

function PanelDialog({ application, today, roles, editsDisabled, onStatusChange, onNoteChange, onRemove }: ApplicationPanelProps) {
  const navigate = useNavigate()
  const location = useLocation()
  const titleId = useId()
  const noteId = useId()
  const historyId = useId()
  const noteLimitId = useId()
  const dialogRef = useRef<HTMLDialogElement>(null)
  const savedNote = useRef(application.note)
  const [note, setNote] = useState(application.note)
  const saveOnUnmount = useRef<() => void>(() => undefined)
  const pressedInside = useRef(false)
  const area = roles.find((role) => role.id === application.role_type)?.label
  const atNoteLimit = note.length >= NOTE_MAX_CHARS

  function saveNote() {
    if (note === savedNote.current) return
    savedNote.current = note
    onNoteChange(application.id, note)
  }

  useEffect(() => {
    saveOnUnmount.current = saveNote
  })

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
    const root = document.documentElement
    const previousOverflow = root.style.overflow
    root.style.overflow = 'hidden'
    return () => {
      root.style.overflow = previousOverflow
      saveOnUnmount.current()
      if (!document.activeElement || document.activeElement === document.body) rowOrAddButton(application.id)?.focus()
    }
  }, [application.id])

  function leave(focusTarget: HTMLElement | null) {
    dialogRef.current?.close()
    focusTarget?.focus()
    if (openedFromList(location.state)) void navigate(-1)
    else void navigate(`${APPLICATIONS_PATH}${location.search}`, { replace: true })
  }

  function close() {
    leave(rowOrAddButton(application.id))
  }

  function remove() {
    const focusTarget = neighborButton(application.id)
    onRemove(application)
    leave(focusTarget)
  }

  function onCancel(event: SyntheticEvent<HTMLDialogElement>) {
    event.preventDefault()
    close()
  }

  function onPointerDown(event: PointerEvent<HTMLDialogElement>) {
    pressedInside.current = event.target !== event.currentTarget
  }

  function onBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget && !pressedInside.current) close()
  }

  return (
    <dialog ref={dialogRef} aria-labelledby={titleId} className={styles.dialog} onCancel={onCancel} onPointerDown={onPointerDown} onClick={onBackdropClick}>
      <div className={styles.panel}>
        <div className={styles.top}>
          <div className={styles.heading}>
            <div className={styles.company}>{application.company}</div>
            <h2 id={titleId} className={styles.title}>
              {application.title || 'Role not set'}
            </h2>
            <div className={styles.meta}>
              <span>{application.place || 'Place not set'}</span>
              {area && <span>{area}</span>}
              <span>{application.source ? SOURCE_NAMES[application.source] : 'Added by hand'}</span>
            </div>
          </div>
          <button type="button" aria-label="Close" className={styles.close} onClick={close}>
            <Icon name="close" size={CLOSE_ICON_SIZE} />
          </button>
        </div>

        <div className={styles.links}>
          {application.source && application.url && (
            <a href={application.url} target="_blank" rel="noopener noreferrer" className={styles.adLink}>
              Open the ad on {SOURCE_NAMES[application.source]}
              <Icon name="external" size={LINK_ICON_SIZE} />
            </a>
          )}
          <a
            href={`${COMPANY_SEARCH_URL}${encodeURIComponent(application.company)}`}
            target="_blank"
            rel="noopener noreferrer"
            className={styles.searchLink}
          >
            Look up the company
            <Icon name="external" size={LINK_ICON_SIZE} />
          </a>
        </div>

        <div className={styles.doubleRule} />
        <div className={styles.statusRow}>
          <span className={styles.factLabel}>Status</span>
          <span className={styles.statusMenu}>
            <StatusMenu
              status={application.status}
              disabled={editsDisabled}
              onChange={(status) => onStatusChange(application.id, status)}
            />
          </span>
        </div>
        <dl className={styles.facts}>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Applied</dt>
            <dd className={styles.factValue}>{formatDay(application.applied_at, today)}</dd>
          </div>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Deadline</dt>
            <dd className={styles.factValue}>{application.deadline ? formatDay(application.deadline, today) : 'Not set'}</dd>
          </div>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>Fit</dt>
            <dd className={styles.factValue}>{application.fit ? <FitMark fit={application.fit} /> : <span className={styles.muted}>Not checked</span>}</dd>
          </div>
          <div className={styles.fact}>
            <dt className={styles.factLabel}>CV sent</dt>
            <dd className={styles.factValue}>{application.cv_name ?? 'Not recorded'}</dd>
          </div>
        </dl>

        {application.checks.length > 0 && (
          <>
            <h3 className={styles.sectionLabel}>What the ad asks for</h3>
            <div className={styles.checks}>
              <CheckList checks={application.checks} />
            </div>
          </>
        )}

        <label htmlFor={noteId} className={styles.sectionLabel}>
          Note
        </label>
        <textarea
          id={noteId}
          value={note}
          rows={5}
          readOnly={editsDisabled}
          maxLength={NOTE_MAX_CHARS}
          aria-describedby={atNoteLimit ? noteLimitId : undefined}
          placeholder="e.g. Interview 14 Oct at 10:00, ask about the team"
          className={styles.note}
          onChange={(event) => setNote(event.target.value.slice(0, NOTE_MAX_CHARS))}
          onBlur={saveNote}
        />
        {atNoteLimit && (
          <p id={noteLimitId} className={styles.limit}>
            Note stops at {NOTE_MAX_CHARS} characters.
          </p>
        )}

        <h3 id={historyId} className={styles.sectionLabel}>
          History
        </h3>
        <ul aria-labelledby={historyId} className={styles.history}>
          {application.history.map((entry) => (
            <li key={`${entry.at}-${entry.text}`} className={styles.historyRow}>
              <span className={styles.muted}>{formatDay(entry.at, today)}</span>
              <span>{entry.text}</span>
            </li>
          ))}
        </ul>

        <div className={styles.actions}>
          <button type="button" className={styles.remove} disabled={editsDisabled} onClick={remove}>
            Remove from list
          </button>
          <Button className={`${button.compact} ${button.roomy}`} onClick={close}>
            Done
          </Button>
        </div>
      </div>
    </dialog>
  )
}
