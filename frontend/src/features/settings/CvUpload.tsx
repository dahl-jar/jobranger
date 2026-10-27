import type { ErrorMessage } from '@/features/relay/contract/payload.gen'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { dayInSentence } from '@/features/settings/dayInSentence'
import styles from '@/features/settings/Settings.module.css'
import { UploadNotice } from '@/features/settings/UploadNotice'
import { useFileUpload } from '@/features/settings/useFileUpload'
import type { UploadFile } from '@/features/settings/uploadFile'
import { readErrorText } from '@/features/settings/uploadText'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

const CV_ICON_SIZE = 18

function replyError(code: ErrorMessage['code'], text: Messages): string | undefined {
  switch (code) {
    case 'cv_type':
      return text.settings.upload.wrongType
    case 'cv_too_large':
      return readErrorText('tooLarge', 'CV', text)
    case 'cv_unreadable':
      return text.settings.upload.unreadable
    case 'cv_no_text':
      return text.settings.cv.noText
    case 'cv_parse_failed':
      return text.settings.cv.parseFailed
    default:
      return undefined
  }
}

interface CvUploadProps {
  cvName: string | null
  cvReadAt: string | null
  today: string
  progress: number | undefined
  errorCode: ErrorMessage['code'] | undefined
  onUpload: (file: UploadFile) => void
}

export function CvUpload({ cvName, cvReadAt, today, progress, errorCode, onUpload }: CvUploadProps) {
  const { inputProps, readError } = useFileUpload(onUpload)
  const text = useText()
  const words = text.settings.cv
  const message = readError === undefined ? errorCode && replyError(errorCode, text) : readErrorText(readError, 'CV', text)

  return (
    <SettingsGroup title={words.heading}>
      <div className={styles.toggleRow}>
        <div className={styles.actions}>
          <Icon name="doc" size={CV_ICON_SIZE} />
          <span className={styles.strong}>{cvName ?? words.none}</span>
          {cvReadAt !== null && <span className={styles.grow}>{words.read(dayInSentence(cvReadAt, today, text))}</span>}
        </div>
        <label className={styles.filePicker}>
          {cvName === null ? words.add : words.replace}
          <input {...inputProps} />
        </label>
      </div>
      <UploadNotice progress={progress} message={message} />
    </SettingsGroup>
  )
}
