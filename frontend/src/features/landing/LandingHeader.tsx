import { Link } from 'react-router'
import { APP_BASE, DEMO_BASE } from '@/app/paths'
import styles from '@/features/landing/LandingPage.module.css'
import { NAV_SECTIONS } from '@/features/landing/landingContent'
import { useText } from '@/shared/i18n/languageContext'
import button from '@/shared/ui/button.module.css'

export function LandingNav() {
  const words = useText().landing.nav

  return (
    <nav aria-label={words.label} className={styles.nav}>
      {NAV_SECTIONS.map((link) => (
        <a key={link.href} href={link.href} className={styles.navLink}>
          {words[link.id]}
        </a>
      ))}
    </nav>
  )
}

export function LandingActions() {
  const text = useText()

  return (
    <>
      <Link to={DEMO_BASE} className={styles.navLink}>
        {text.app.demo.link}
      </Link>
      <Link to={APP_BASE} className={`${button.outline} ${button.compact} ${button.medium}`}>
        {text.landing.openApp}
      </Link>
    </>
  )
}
