import type { ContextSnapshot, ErrorMessage } from '@/features/relay/contract/payload.gen'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { dayInSentence } from '@/features/settings/dayInSentence'
import styles from '@/features/settings/Settings.module.css'
import { UploadNotice } from '@/features/settings/UploadNotice'
import { useFileUpload } from '@/features/settings/useFileUpload'
import type { UploadFile } from '@/features/settings/uploadFile'
import { readErrorText } from '@/features/settings/uploadText'
import { formatCount } from '@/shared/format/numbers'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

const DOC_ICON_SIZE = 18
function replyError(code: ErrorMessage['code'], text: Messages): string | undefined {
  switch (code) {
    case 'context_unreadable':
      return text.settings.upload.unreadable
    case 'context_full':
      return text.settings.upload.contextFull
    default:
      return undefined
  }
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
  const text = useText()
  const words = text.settings.context
  const message = readError === undefined ? errorCode && replyError(errorCode, text) : readErrorText(readError, 'file', text)

  return (
    <SettingsGroup title={words.heading}>
      {context.docs.length === 0 ? (
        <p className={`${styles.row} ${styles.grow}`}>{words.empty}</p>
      ) : (
        <ul className={styles.plainList}>
          {context.docs.map((doc) => (
            <li key={doc.id} className={styles.row}>
              <span className={styles.actions}>
                <Icon name="doc" size={DOC_ICON_SIZE} />
                <span className={styles.strong}>{doc.name}</span>
              </span>
              <span className={styles.grow}>
                {words.facts(doc.chars, formatCount(doc.chars, text), dayInSentence(doc.added_at, today, text))}
              </span>
              <button type="button" className={styles.textButton} aria-label={text.ui.removeItem(doc.name)} onClick={() => onRemove(doc.id)}>
                <Icon name="close" />
                {text.common.remove}
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className={styles.toolRow}>
        <label className={styles.filePicker}>
          <Icon name="plus" />
          {words.add}
          <input {...inputProps} disabled={isFull} />
        </label>
        <span className={styles.hint}>
          {isFull ? words.full(context.docs_limit) : words.hint(context.docs_limit)}
        </span>
      </div>
      <UploadNotice progress={progress} message={message} />
    </SettingsGroup>
  )
}
