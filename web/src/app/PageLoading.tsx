import styles from '@/app/Notice.module.css'
import { useText } from '@/shared/i18n/languageContext'

export function PageLoading() {
  const text = useText()
  return (
    <p role="status" className={styles.loading}>
      {text.app.loading}
    </p>
  )
}
