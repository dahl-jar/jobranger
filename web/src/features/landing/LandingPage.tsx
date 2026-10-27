import { Link } from 'react-router'
import { APP_BASE, DEMO_BASE } from '@/app/paths'
import { ExampleMatch } from '@/features/landing/ExampleMatch'
import { Faq } from '@/features/landing/Faq'
import { InstallSteps } from '@/features/landing/InstallSteps'
import styles from '@/features/landing/LandingPage.module.css'
import { SOURCE_URL } from '@/shared/config'
import { useText } from '@/shared/i18n/languageContext'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import { SectionLabel } from '@/shared/ui/SectionLabel'
import { usePageTitle } from '@/shared/usePageTitle'

const CLAIM_ICON_SIZE = 15
const PRIVACY_ICON_SIZE = 16

const HOW_NUMBER_DIGITS = 2

function Hero() {
  const text = useText()
  const words = text.landing

  return (
    <section className={styles.hero}>
      <div className={styles.heroText}>
        <p className={styles.early}>
          <Icon name="clock" />
          {words.early}
        </p>
        <h1 tabIndex={-1} className={styles.headline}>{words.headline}</h1>
        <p className={styles.lead}>{words.lead}</p>
        <div className={styles.actions}>
          <Link to="/setup" aria-label={words.installLabel} className={`${button.primary} ${button.large}`}>
            {words.install}
            <Icon name="forward" />
          </Link>
          <Link to={DEMO_BASE} className={`${button.outline} ${button.large}`}>
            {text.app.demo.view}
          </Link>
          <a href="#how" className={styles.howLink}>
            {words.howLink}
            <Icon name="down" />
          </a>
        </div>
        <ul className={styles.claims}>
          {words.claims.map((claim) => (
            <li key={claim} className={styles.claim}>
              <span className={styles.ok}>
                <Icon name="check" size={CLAIM_ICON_SIZE} />
              </span>
              {claim}
            </li>
          ))}
        </ul>
      </div>
      <ExampleMatch />
    </section>
  )
}

function HowItWorks() {
  const words = useText().landing

  return (
    <section id="how" className={styles.section}>
      <SectionLabel>{words.howHeading}</SectionLabel>
      {words.howSteps.map((step, index) => (
        <div key={index} className={styles.row}>
          <div className={styles.number}>{String(index + 1).padStart(HOW_NUMBER_DIGITS, '0')}</div>
          <h3 className={styles.howTitle}>{step.title}</h3>
          <p className={styles.howBody}>{step.body}</p>
        </div>
      ))}
    </section>
  )
}

function Install() {
  const words = useText().landing

  return (
    <section id="install" className={styles.section}>
      <div className={styles.installHeader}>
        <h2 className={styles.heading}>{words.installHeading}</h2>
        <span className={styles.platforms}>{words.platforms}</span>
      </div>
      <div className={styles.installRule} />
      <InstallSteps />
    </section>
  )
}

function Cost() {
  const words = useText().landing

  return (
    <section id="free" className={styles.section}>
      <h2 className={styles.heading}>{words.costHeading}</h2>
      <p className={styles.cost}>{words.cost}</p>
    </section>
  )
}

function DataStorage() {
  const words = useText().landing

  return (
    <section className={`${styles.section} ${styles.storage}`}>
      <h2 className={`${styles.heading} ${styles.storageHeading}`}>{words.storageHeading}</h2>
      <ul className={styles.privacy}>
        {words.privacy.map((line) => (
          <li key={line} className={styles.privacyLine}>
            <span className={styles.ok}>
              <Icon name="check" size={PRIVACY_ICON_SIZE} />
            </span>
            {line}
          </li>
        ))}
      </ul>
    </section>
  )
}

function Questions() {
  return (
    <section id="faq" className={styles.section}>
      <SectionLabel>{useText().landing.questionsHeading}</SectionLabel>
      <Faq />
    </section>
  )
}

function Footer() {
  const words = useText().landing

  return (
    <footer className={styles.footer}>
      <div className={styles.footerRule} />
      <div className={styles.footerRow}>
        <span>{words.footer}</span>
        <span className={styles.footerLinks}>
          <a href={SOURCE_URL}>{words.sourceCode}</a>
          <Link to="/setup">{words.setupWalkthrough}</Link>
          <Link to={APP_BASE}>{words.openApp}</Link>
        </span>
      </div>
    </footer>
  )
}

export function LandingPage() {
  usePageTitle()

  return (
    <>
      <main>
        <Hero />
        <HowItWorks />
        <Install />
        <Cost />
        <DataStorage />
        <Questions />
      </main>
      <Footer />
    </>
  )
}
