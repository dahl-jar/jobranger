import { useId, useRef, useState } from 'react'
import type { ErrorMessage, GuidanceSnapshot } from '@/features/relay/contract/payload.gen'
import styles from '@/features/settings/Settings.module.css'
import { useDraft } from '@/features/settings/useDraft'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'
import { Icon } from '@/shared/ui/Icon'

const GUIDANCE_ROWS = 12

interface EditorProps {
  id: string
  guidance: GuidanceSnapshot
  editsDisabled: boolean
  onSave: (text: string) => void
  onReset: () => void
}

function GuidanceEditor({ id, guidance, editsDisabled, onSave, onReset }: EditorProps) {
  const counterId = useId()
  const [draft, setDraft] = useDraft(guidance.text)
  const trimmed = draft.trim()
  const isOver = draft.length > guidance.limit
  const canSave = !editsDisabled && trimmed !== '' && trimmed !== guidance.text && !isOver
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const text = useText()
  const words = text.settings.matching

  function save() {
    textareaRef.current?.focus()
    onSave(trimmed)
  }

  return (
    <div id={id}>
      <textarea
        ref={textareaRef}
        aria-label={words.guidance}
        aria-describedby={counterId}
        className={styles.textarea}
        rows={GUIDANCE_ROWS}
        spellCheck={false}
        readOnly={editsDisabled}
        placeholder={words.placeholder}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
      />
      <div className={styles.footer}>
        <span id={counterId} className={isOver ? `${styles.counter} ${styles.over}` : styles.counter}>
          {draft.length} / {guidance.limit}
        </span>
        <div className={styles.actions}>
          <button type="button" className={styles.textButton} disabled={editsDisabled || guidance.is_default} onClick={() => onReset()}>
            {words.reset}
          </button>
          <Button size={44} disabled={!canSave} onClick={save}>
            {text.common.save}
          </Button>
        </div>
      </div>
      <p className={styles.note}>{words.note}</p>
    </div>
  )
}

interface MatchingSettingsProps {
  guidance: GuidanceSnapshot
  errorCode: ErrorMessage['code'] | undefined
  editsDisabled: boolean
  onSave: (text: string) => void
  onReset: () => void
}

export function MatchingSettings({ guidance, errorCode, editsDisabled, onSave, onReset }: MatchingSettingsProps) {
  const editorId = useId()
  const [isOpen, setOpen] = useState(false)
  const text = useText()
  const words = text.settings.matching
  const isEdited = !guidance.is_default

  return (
    <div className={styles.group}>
      <div className={styles.rule} />
      <div className={styles.toggleRow}>
        <div className={styles.actions}>
          <span className={styles.strong}>{words.guidance}</span>
          <span aria-hidden="true">·</span>
          <span className={isEdited ? `${styles.state} ${styles.edited}` : styles.state}>
            <Icon name={isEdited ? 'pencil' : 'page'} />
            {isEdited ? words.edited : words.default}
          </span>
        </div>
        <Button
          variant="quiet"
          size={40}
          aria-label={isOpen ? words.closeGuidance : words.editGuidance}
          aria-expanded={isOpen}
          aria-controls={editorId}
          onClick={() => setOpen(!isOpen)}
        >
          {isOpen ? text.common.close : text.common.edit}
        </Button>
      </div>
      {isOpen && <GuidanceEditor id={editorId} guidance={guidance} editsDisabled={editsDisabled} onSave={onSave} onReset={onReset} />}
      {errorCode === 'guidance_empty' && (
        <p role="alert" className={styles.error}>
          {words.empty}
        </p>
      )}
    </div>
  )
}
