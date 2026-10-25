import { isValidElement, useEffect, useRef, type ReactElement } from 'react'
import { Link, Outlet, ScrollRestoration, useLocation, useMatches } from 'react-router'
import styles from '@/app/PublicLayout.module.css'
import { scrollKey } from '@/app/scrollPositions'
import { ThemeSwitch } from '@/app/theme/ThemeSwitch'
import { Icon } from '@/shared/ui/Icon'

export interface PublicLayoutHandle {
  subtitle?: string
  showHome?: boolean
  wide?: boolean
  nav?: ReactElement
  action?: ReactElement
}

interface LayoutOptions {
  subtitle: string | undefined
  showHome: boolean
  wide: boolean
  nav: ReactElement | undefined
  action: ReactElement | undefined
}

const BACK_ICON_SIZE = 15
const PAGE_HEADING_SELECTOR = 'main h1'

function useHeadingFocus(): void {
  const { pathname } = useLocation()
  const shownPath = useRef(pathname)

  useEffect(() => {
    if (shownPath.current === pathname) return
    shownPath.current = pathname
    document.querySelector<HTMLElement>(PAGE_HEADING_SELECTOR)?.focus({ preventScroll: true })
  }, [pathname])
}

function readHandle(handle: unknown): LayoutOptions {
  const fields = handle instanceof Object ? handle : {}
  return {
    subtitle: 'subtitle' in fields && typeof fields.subtitle === 'string' ? fields.subtitle : undefined,
    showHome: 'showHome' in fields && fields.showHome === true,
    wide: 'wide' in fields && fields.wide === true,
    nav: 'nav' in fields && isValidElement(fields.nav) ? fields.nav : undefined,
    action: 'action' in fields && isValidElement(fields.action) ? fields.action : undefined,
  }
}

export function PublicLayout() {
  const { subtitle, showHome, wide, nav, action } = readHandle(useMatches().at(-1)?.handle)
  useHeadingFocus()

  return (
    <div className={`${styles.page} ${wide ? styles.wide : ''}`}>
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
              Home
            </Link>
          )}
        </div>
      </header>
      <div className={styles.doubleRule} />
      <Outlet />
      <ScrollRestoration getKey={scrollKey} />
    </div>
  )
}
