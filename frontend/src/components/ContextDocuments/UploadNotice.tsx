import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { useText } from '@/hooks/useLanguage'
import { PERCENT_MAX } from '@/constants/percentMax'

export function UploadNotice({ progress, message }: { progress: number | undefined; message: string | undefined }) {
  const text = useText()

  return (
    <>
      {progress !== undefined && (
        <p role="status" className={styles.note}>
          {text.settings.upload.sending(text.numbers.percent(Math.round(progress * PERCENT_MAX)))}
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
