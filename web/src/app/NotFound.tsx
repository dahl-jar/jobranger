import { Link } from 'react-router'
import styles from '@/app/Notice.module.css'
import { usePageTitle } from '@/shared/usePageTitle'

export function NotFound() {
  usePageTitle('No page here')

  return (
    <main className={styles.notice}>
      <h1 tabIndex={-1} className={styles.title}>No page here.</h1>
      <p className={styles.links}>
        <Link to="/">Home</Link>
      </p>
    </main>
  )
}
