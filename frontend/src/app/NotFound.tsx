import { Link } from 'react-router'
import { useAppBase } from '@/app/paths'
import styles from '@/app/Notice.module.css'
import { useText } from '@/shared/i18n/languageContext'
import { usePageTitle } from '@/shared/usePageTitle'

export function NotFound() {
  const text = useText()
  const base = useAppBase()
  usePageTitle(text.app.notFound.title)

  return (
    <main className={styles.notice}>
      <h1 tabIndex={-1} className={styles.title}>{text.app.notFound.heading}</h1>
      <p className={styles.links}>
        <Link to="/">{text.common.home}</Link>
        <Link to={base}>{text.landing.openApp}</Link>
      </p>
    </main>
  )
}
