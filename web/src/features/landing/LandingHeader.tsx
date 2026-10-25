import { Link } from 'react-router'
import styles from '@/features/landing/LandingPage.module.css'
import button from '@/shared/ui/button.module.css'

const NAV_LINKS = [
  { href: '#how', label: 'How it works' },
  { href: '#install', label: 'Install' },
  { href: '#free', label: 'Free' },
  { href: '#faq', label: 'Questions' },
] as const

export function LandingNav() {
  return (
    <nav aria-label="Main" className={styles.nav}>
      {NAV_LINKS.map((link) => (
        <a key={link.href} href={link.href} className={styles.navLink}>
          {link.label}
        </a>
      ))}
    </nav>
  )
}

export function OpenAppLink() {
  return (
    <Link to="/app" className={`${button.outline} ${button.compact} ${button.medium}`}>
      Open the app
    </Link>
  )
}
