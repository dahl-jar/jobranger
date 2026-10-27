import type { ErrorMessage } from '@/features/relay/contract/payload.gen'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { dayInSentence } from '@/features/settings/dayInSentence'
import styles from '@/features/settings/Settings.module.css'
import { UploadNotice } from '@/features/settings/UploadNotice'
import { useFileUpload } from '@/features/settings/useFileUpload'
import type { UploadFile } from '@/features/settings/uploadFile'
import { readErrorText, UNREADABLE, WRONG_TYPE } from '@/features/settings/uploadText'
import { Icon } from '@/shared/ui/Icon'

const CV_ICON_SIZE = 18

const REPLY_ERRORS: Partial<Record<ErrorMessage['code'], string>> = {
  cv_type: WRONG_TYPE,
  cv_too_large: readErrorText('tooLarge', 'CV'),
  cv_unreadable: UNREADABLE,
  cv_no_text: 'This PDF has no text. Try another file or paste a summary in the terminal.',
  cv_parse_failed: "jobranger couldn't read your CV. Try again or edit the rows by hand.",
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
  const message = readError === undefined ? errorCode && REPLY_ERRORS[errorCode] : readErrorText(readError, 'CV')

  return (
    <SettingsGroup title="CV">
      <div className={styles.toggleRow}>
        <div className={styles.actions}>
          <Icon name="doc" size={CV_ICON_SIZE} />
          <span className={styles.strong}>{cvName ?? 'No CV yet'}</span>
          {cvReadAt !== null && <span className={styles.grow}>read {dayInSentence(cvReadAt, today)}</span>}
        </div>
        <label className={styles.filePicker}>
          {cvName === null ? 'Add CV' : 'Replace CV'}
          <input {...inputProps} />
        </label>
      </div>
      <UploadNotice progress={progress} message={message} />
    </SettingsGroup>
  )
}
