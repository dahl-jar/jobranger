import { useEffect, useRef } from 'react'
import { Link, useHref, useLocation, useRouteError } from 'react-router'
import styles from '@/app/Notice.module.css'
import { reloadOnce, RouteImportError } from '@/app/routeImport'
import { usePageTitle } from '@/shared/usePageTitle'

const BROKEN_TITLE = 'Something broke'
const NOT_LOADED_TITLE = 'Could not load this page'

export function RouteError() {
  const error = useRouteError()
  const notLoaded = error instanceof RouteImportError
  const location = useLocation()
  const href = useHref(location)
  const heading = useRef<HTMLHeadingElement>(null)
  usePageTitle(notLoaded ? NOT_LOADED_TITLE : BROKEN_TITLE)

  useEffect(() => {
    heading.current?.focus()
  }, [])

  useEffect(() => {
    if (notLoaded) reloadOnce(href, error.cause, location.key)
  }, [notLoaded, error, href, location.key])

  return (
    <main className={`${styles.notice} ${styles.standalone}`}>
      <h1 ref={heading} tabIndex={-1} className={styles.title}>
        {notLoaded ? `${NOT_LOADED_TITLE}.` : 'Something broke on this page.'}
      </h1>
      {notLoaded && <p className={styles.detail}>Check your connection and reload.</p>}
      <div className={styles.actions}>
        <button type="button" className={styles.reload} onClick={() => window.location.reload()}>
          Reload
        </button>
        <Link to="/" className={styles.home}>
          Home
        </Link>
      </div>
    </main>
  )
}
