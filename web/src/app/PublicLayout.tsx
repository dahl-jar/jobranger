import { isValidElement, useEffect, useRef, useState, type ReactElement } from 'react'
import { Link, Outlet, ScrollRestoration, useLocation, useMatches, useNavigation } from 'react-router'
import { PageLoading } from '@/app/PageLoading'
import styles from '@/app/PublicLayout.module.css'
import { canStoreScroll, scrollKey } from '@/app/scrollPositions'
import { ThemeSwitch } from '@/app/theme/ThemeSwitch'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

export interface PublicLayoutHandle {
  subtitle?: string | ReactElement
  showHome?: boolean
  wide?: boolean
  nav?: ReactElement
  action?: ReactElement
}

interface LayoutOptions {
  subtitle: string | ReactElement | undefined
  showHome: boolean
  wide: boolean
  nav: ReactElement | undefined
  action: ReactElement | undefined
}

const BACK_ICON_SIZE = 15
const PAGE_HEADING_SELECTOR = 'main h1'
const BUSY_DELAY_MS = 300

function useHeadingFocus(): void {
  const { pathname } = useLocation()
  const shownPath = useRef(pathname)

  useEffect(() => {
    if (shownPath.current === pathname) return
    shownPath.current = pathname
    document.querySelector<HTMLElement>(PAGE_HEADING_SELECTOR)?.focus({ preventScroll: true })
  }, [pathname])
}

function useSlowNavigation(): boolean {
  const loading = useNavigation().state === 'loading'
  const [slow, setSlow] = useState(false)

  useEffect(() => {
    if (!loading) return undefined
    const timer = window.setTimeout(() => setSlow(true), BUSY_DELAY_MS)
    return () => {
      window.clearTimeout(timer)
      setSlow(false)
    }
  }, [loading])

  return slow
}

function readSubtitle(subtitle: unknown): string | ReactElement | undefined {
  return typeof subtitle === 'string' || isValidElement(subtitle) ? subtitle : undefined
}

function readHandle(handle: unknown): LayoutOptions {
  const fields = handle instanceof Object ? handle : {}
  return {
    subtitle: 'subtitle' in fields ? readSubtitle(fields.subtitle) : undefined,
    showHome: 'showHome' in fields && fields.showHome === true,
    wide: 'wide' in fields && fields.wide === true,
    nav: 'nav' in fields && isValidElement(fields.nav) ? fields.nav : undefined,
    action: 'action' in fields && isValidElement(fields.action) ? fields.action : undefined,
  }
}

export function PublicLayout() {
  const { subtitle, showHome, wide, nav, action } = readHandle(useMatches().at(-1)?.handle)
  useHeadingFocus()
  const busy = useSlowNavigation()
  const text = useText()
  const [storesScroll] = useState(canStoreScroll)

  return (
    <div className={`${styles.page} ${wide ? styles.wide : ''}`} aria-busy={busy}>
      <header className={styles.header}>
        <div className={styles.brand}>
          <Link to="/" className={styles.logo}>
            jobranger
          </Link>
          {subtitle !== undefined && <span className={styles.subtitle}>{subtitle}</span>}
        </div>
        <div className={styles.tools}>
          {nav}
          <ThemeSwitch variant="icons" />
          {action}
          {showHome && (
            <Link to="/" className={styles.home}>
              <Icon name="back" size={BACK_ICON_SIZE} />
              {text.common.home}
            </Link>
          )}
        </div>
      </header>
      <div className={styles.doubleRule} />
      {busy && <PageLoading />}
      <Outlet />
      {storesScroll && <ScrollRestoration getKey={scrollKey} />}
    </div>
  )
}
