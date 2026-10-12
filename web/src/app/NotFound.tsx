import { Link } from 'react-router'
import styles from '@/app/Notice.module.css'

export function NotFound() {
  return (
    <main className={styles.notice}>
      <h1 className={styles.title}>No page here.</h1>
      <p className={styles.links}>
        <Link to="/">Home</Link>
      </p>
    </main>
  )
}
