import styles from '@/app/Notice.module.css'

export function PageLoading() {
  return (
    <p role="status" className={styles.loading}>
      Loading...
    </p>
  )
}
