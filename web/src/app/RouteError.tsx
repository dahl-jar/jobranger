import styles from '@/app/Notice.module.css'

export function RouteError() {
  return (
    <main className={`${styles.notice} ${styles.standalone}`}>
      <h1 className={styles.title}>Something broke on this page.</h1>
      <button type="button" className={styles.reload} onClick={() => window.location.reload()}>
        Reload
      </button>
    </main>
  )
}
