import { Link } from 'react-router'
import { APP_BASE, DEMO_BASE } from '@/helpers/appPaths'
import styles from '@/components/LandingPage/LandingPage.module.css'
import { NAV_SECTIONS } from '@/components/LandingPage/landingContent'
import { useText } from '@/hooks/useLanguage'
import button from '@/components/Button/button.module.css'

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
