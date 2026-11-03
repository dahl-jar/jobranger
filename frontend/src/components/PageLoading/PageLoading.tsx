import styles from '@/components/Notice/Notice.module.css'
import { useText } from '@/hooks/useLanguage'

export function PageLoading() {
  const text = useText()
  return (
    <p role="status" className={styles.loading}>
      {text.app.loading}
    </p>
  )
}
