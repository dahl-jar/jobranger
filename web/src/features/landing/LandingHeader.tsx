import { Link } from 'react-router'
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

export function OpenAppLink() {
  return (
    <Link to="/app" className={`${button.outline} ${button.compact} ${button.medium}`}>
      {useText().landing.openApp}
    </Link>
  )
}
