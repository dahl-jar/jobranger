import { Link, Outlet, useMatches } from 'react-router'
import styles from '@/app/PublicLayout.module.css'
import { ThemeSwitch } from '@/app/theme/ThemeSwitch'
import { Icon } from '@/shared/ui/Icon'

export interface PublicLayoutHandle {
  subtitle?: string
  showHome?: boolean
}

const BACK_ICON_SIZE = 15

function readHandle(handle: unknown): { subtitle: string | undefined; showHome: boolean } {
  if (typeof handle !== 'object' || handle === null) return { subtitle: undefined, showHome: false }
  const subtitle = 'subtitle' in handle && typeof handle.subtitle === 'string' ? handle.subtitle : undefined
  const showHome = 'showHome' in handle && handle.showHome === true
  return { subtitle, showHome }
}

export function PublicLayout() {
  const { subtitle, showHome } = readHandle(useMatches().at(-1)?.handle)

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div className={styles.brand}>
          <Link to="/" className={styles.logo}>
            jobranger
          </Link>
          {subtitle !== undefined && <span className={styles.subtitle}>{subtitle}</span>}
        </div>
        <div className={styles.tools}>
          <ThemeSwitch variant="icons" />
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
    </div>
  )
}
