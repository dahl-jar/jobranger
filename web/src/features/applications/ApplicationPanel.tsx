import { useEffect, useId, useRef, useState, type MouseEvent, type PointerEvent, type SyntheticEvent } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { useAppBase } from '@/app/paths'
import styles from '@/features/applications/ApplicationPanel.module.css'
import { applicationsPath, openedFromList } from '@/features/applications/applicationsPath'
import { NOTE_MAX_CHARS } from '@/features/applications/limits'
import { neighborButton, rowOrAddButton } from '@/features/applications/rowFocus'
import { StatusMenu } from '@/features/applications/StatusMenu'
import type { Application, ApplicationStatus, RoleOption } from '@/features/relay/contract/payload.gen'
import { formatDay } from '@/shared/format/dates'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'
import { CheckList } from '@/shared/ui/CheckList'
import { FitMark } from '@/shared/ui/FitMark'
import { Icon } from '@/shared/ui/Icon'
import { SOURCE_NAMES } from '@/shared/labels/sources'

const COMPANY_SEARCH_URL = 'https://www.google.com/search?q='

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

interface PanelPartProps {
  application: Application
  today: string
}

interface PanelHeadingProps {
  application: Application
  roles: readonly RoleOption[]
  titleId: string
  onClose: () => void
}

function PanelHeading({ application, roles, titleId, onClose }: PanelHeadingProps) {
  const text = useText()
  const words = text.applications.panel
  const area = roles.find((role) => role.id === application.role_type)?.label
  return (
    <div className={styles.top}>
      <div className={styles.heading}>
        <div className={styles.company}>{application.company}</div>
        <h2 id={titleId} className={styles.title}>
          {application.title || text.applications.roleNotSet}
        </h2>
        <div className={styles.meta}>
          <span>{application.place || words.placeNotSet}</span>
          {area && <span>{area}</span>}
          <span>{application.source ? SOURCE_NAMES[application.source] : words.byHand}</span>
        </div>
      </div>
      <button type="button" aria-label={text.common.close} className={styles.close} onClick={onClose}>
        <Icon name="close" size={CLOSE_ICON_SIZE} />
      </button>
    </div>
  )
}

function PanelLinks({ application }: Pick<PanelPartProps, 'application'>) {
  const words = useText().applications.panel
  return (
    <div className={styles.links}>
      {application.source && application.url && (
        <a href={application.url} target="_blank" rel="noopener noreferrer" className={styles.adLink}>
          {words.openAd(SOURCE_NAMES[application.source])}
          <Icon name="external" size={LINK_ICON_SIZE} />
        </a>
      )}
      <a
        href={`${COMPANY_SEARCH_URL}${encodeURIComponent(application.company)}`}
        target="_blank"
        rel="noopener noreferrer"
        className={styles.searchLink}
      >
        {words.lookUp}
        <Icon name="external" size={LINK_ICON_SIZE} />
      </a>
    </div>
  )
}

function PanelFacts({ application, today }: PanelPartProps) {
  const text = useText()
  const words = text.applications.panel
  return (
    <dl className={styles.facts}>
      <div className={styles.fact}>
        <dt className={styles.factLabel}>{words.applied}</dt>
        <dd className={styles.factValue}>{formatDay(application.applied_at, today, text)}</dd>
      </div>
      <div className={styles.fact}>
        <dt className={styles.factLabel}>{words.deadline}</dt>
        <dd className={styles.factValue}>{application.deadline ? formatDay(application.deadline, today, text) : words.notSet}</dd>
      </div>
      <div className={styles.fact}>
        <dt className={styles.factLabel}>{words.fit}</dt>
        <dd className={styles.factValue}>{application.fit ? <FitMark fit={application.fit} /> : <span className={styles.muted}>{words.notChecked}</span>}</dd>
      </div>
      <div className={styles.fact}>
        <dt className={styles.factLabel}>{words.cvSent}</dt>
        <dd className={styles.factValue}>{application.cv_name ?? words.notRecorded}</dd>
      </div>
    </dl>
  )
}

interface NoteFieldProps {
  note: string
  editsDisabled: boolean
  onChange: (note: string) => void
  onBlur: () => void
}

function NoteField({ note, editsDisabled, onChange, onBlur }: NoteFieldProps) {
  const noteId = useId()
  const noteLimitId = useId()
  const words = useText().applications.panel
  const atNoteLimit = note.length >= NOTE_MAX_CHARS
  return (
    <>
      <label htmlFor={noteId} className={styles.sectionLabel}>
        {words.note}
      </label>
      <textarea
        id={noteId}
        value={note}
        rows={5}
        readOnly={editsDisabled}
        maxLength={NOTE_MAX_CHARS}
        aria-describedby={atNoteLimit ? noteLimitId : undefined}
        placeholder={words.notePlaceholder}
        className={styles.note}
        onChange={(event) => onChange(event.target.value.slice(0, NOTE_MAX_CHARS))}
        onBlur={onBlur}
      />
      {atNoteLimit && (
        <p id={noteLimitId} className={styles.limit}>
          {words.noteStops(NOTE_MAX_CHARS)}
        </p>
      )}
    </>
  )
}

function PanelHistory({ application, today }: PanelPartProps) {
  const historyId = useId()
  const text = useText()
  return (
    <>
      <h3 id={historyId} className={styles.sectionLabel}>
        {text.applications.panel.history}
      </h3>
      <ul aria-labelledby={historyId} className={styles.history}>
        {application.history.map((entry) => (
          <li key={`${entry.at}-${entry.text}`} className={styles.historyRow}>
            <span className={styles.muted}>{formatDay(entry.at, today, text)}</span>
            <span>{entry.text}</span>
          </li>
        ))}
      </ul>
    </>
  )
}

function PanelDialog({ application, today, roles, editsDisabled, onStatusChange, onNoteChange, onRemove }: ApplicationPanelProps) {
  const navigate = useNavigate()
  const location = useLocation()
  const base = useAppBase()
  const titleId = useId()
  const dialogRef = useRef<HTMLDialogElement>(null)
  const savedNote = useRef(application.note)
  const [note, setNote] = useState(application.note)
  const saveOnUnmount = useRef<() => void>(() => undefined)
  const pressedInside = useRef(false)
  const words = useText().applications.panel

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
    else void navigate(`${applicationsPath(base)}${location.search}`, { replace: true })
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
        <PanelHeading application={application} roles={roles} titleId={titleId} onClose={close} />
        <PanelLinks application={application} />

        <div className={styles.doubleRule} />
        <div className={styles.statusRow}>
          <span className={styles.factLabel}>{words.status}</span>
          <span className={styles.statusMenu}>
            <StatusMenu
              status={application.status}
              disabled={editsDisabled}
              onChange={(status) => onStatusChange(application.id, status)}
            />
          </span>
        </div>
        <PanelFacts application={application} today={today} />

        {application.checks.length > 0 && (
          <>
            <h3 className={styles.sectionLabel}>{words.asks}</h3>
            <div className={styles.checks}>
              <CheckList checks={application.checks} />
            </div>
          </>
        )}

        <NoteField note={note} editsDisabled={editsDisabled} onChange={setNote} onBlur={saveNote} />
        <PanelHistory application={application} today={today} />

        <div className={styles.actions}>
          <button type="button" className={styles.remove} disabled={editsDisabled} onClick={remove}>
            {words.remove}
          </button>
          <Button className={`${button.compact} ${button.roomy}`} onClick={close}>
            {words.done}
          </Button>
        </div>
      </div>
    </dialog>
  )
}

export function ApplicationPanel(props: ApplicationPanelProps) {
  return <PanelDialog key={props.application.id} {...props} />
}
