import styles from '@/features/settings/Settings.module.css'
import { PERCENT_MAX } from '@/shared/ui/ProgressBar'

export function UploadNotice({ progress, message }: { progress: number | undefined; message: string | undefined }) {
  return (
    <>
      {progress !== undefined && (
        <p role="status" className={styles.note}>
          Sending {Math.round(progress * PERCENT_MAX)}%
        </p>
      )}
      {message !== undefined && (
        <p role="alert" className={styles.error}>
          {message}
        </p>
      )}
    </>
  )
}
