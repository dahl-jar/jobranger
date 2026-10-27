import type { ContextSnapshot, ErrorMessage } from '@/features/relay/contract/payload.gen'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { dayInSentence } from '@/features/settings/dayInSentence'
import styles from '@/features/settings/Settings.module.css'
import { UploadNotice } from '@/features/settings/UploadNotice'
import { useFileUpload } from '@/features/settings/useFileUpload'
import type { UploadFile } from '@/features/settings/uploadFile'
import { CONTEXT_FULL, readErrorText, UNREADABLE } from '@/features/settings/uploadText'
import { Icon } from '@/shared/ui/Icon'

const DOC_ICON_SIZE = 18
const COUNT_LOCALE = 'en-US'

const REPLY_ERRORS: Partial<Record<ErrorMessage['code'], string>> = {
  context_unreadable: UNREADABLE,
  context_full: CONTEXT_FULL,
}

interface ContextDocumentsProps {
  context: ContextSnapshot
  today: string
  progress: number | undefined
  errorCode: ErrorMessage['code'] | undefined
  onUpload: (file: UploadFile) => void
  onRemove: (docId: string) => void
}

export function ContextDocuments({ context, today, progress, errorCode, onUpload, onRemove }: ContextDocumentsProps) {
  const { inputProps, readError } = useFileUpload(onUpload)
  const isFull = context.docs.length >= context.docs_limit
  const message = readError === undefined ? errorCode && REPLY_ERRORS[errorCode] : readErrorText(readError, 'file')

  return (
    <SettingsGroup title="Context documents">
      {context.docs.length === 0 ? (
        <p className={`${styles.row} ${styles.grow}`}>No documents yet. Add notes or a project write-up the CV leaves out.</p>
      ) : (
        <ul className={styles.plainList}>
          {context.docs.map((doc) => (
            <li key={doc.id} className={styles.row}>
              <span className={styles.actions}>
                <Icon name="doc" size={DOC_ICON_SIZE} />
                <span className={styles.strong}>{doc.name}</span>
              </span>
              <span className={styles.grow}>
                {doc.chars.toLocaleString(COUNT_LOCALE)} characters · added {dayInSentence(doc.added_at, today)}
              </span>
              <button type="button" className={styles.textButton} aria-label={`Remove ${doc.name}`} onClick={() => onRemove(doc.id)}>
                <Icon name="close" />
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className={styles.toolRow}>
        <label className={styles.filePicker}>
          <Icon name="plus" />
          Add document
          <input {...inputProps} disabled={isFull} />
        </label>
        <span className={styles.hint}>
          {isFull ? `At most ${context.docs_limit} documents.` : `PDF, TXT or MD, up to ${context.docs_limit} documents. Only the text is kept.`}
        </span>
      </div>
      <UploadNotice progress={progress} message={message} />
    </SettingsGroup>
  )
}
