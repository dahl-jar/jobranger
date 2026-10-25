import { Link } from 'react-router'
import { ExampleMatch } from '@/features/landing/ExampleMatch'
import { Faq } from '@/features/landing/Faq'
import { InstallSteps } from '@/features/landing/InstallSteps'
import styles from '@/features/landing/LandingPage.module.css'
import { CLAIMS, HOW_STEPS, PRIVACY_LINES } from '@/features/landing/landingContent'
import { SOURCE_URL } from '@/shared/config'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import { SectionLabel } from '@/shared/ui/SectionLabel'
import { usePageTitle } from '@/shared/usePageTitle'

const CLAIM_ICON_SIZE = 15
const PRIVACY_ICON_SIZE = 16

function Hero() {
  return (
    <section className={styles.hero}>
      <div className={styles.heroText}>
        <p className={styles.early}>
          <Icon name="clock" />
          Early version. More fields and settings are in progress.
        </p>
        <h1 tabIndex={-1} className={styles.headline}>jobranger checks Norwegian software and data engineering ads against your CV.</h1>
        <p className={styles.lead}>
          On a schedule or by hand, it reads NAV, company career pages, FINN and LinkedIn (opt-in), and lists the ads that fit your CV, with the reason.
        </p>
        <div className={styles.actions}>
          <Link to="/setup" aria-label="Install, setup walkthrough" className={`${button.primary} ${button.large}`}>
            Install
            <Icon name="forward" />
          </Link>
          <a href="#how" className={styles.howLink}>
            How it works
            <Icon name="down" />
          </a>
        </div>
        <ul className={styles.claims}>
          {CLAIMS.map((claim) => (
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
  return (
    <section id="how" className={styles.section}>
      <SectionLabel>How it works</SectionLabel>
      {HOW_STEPS.map((step) => (
        <div key={step.number} className={styles.row}>
          <div className={styles.number}>{step.number}</div>
          <h3 className={styles.howTitle}>{step.title}</h3>
          <p className={styles.howBody}>{step.body}</p>
        </div>
      ))}
    </section>
  )
}

function Install() {
  return (
    <section id="install" className={styles.section}>
      <div className={styles.installHeader}>
        <h2 className={styles.heading}>Install and set up</h2>
        <span className={styles.platforms}>macOS or Linux. Windows through WSL.</span>
      </div>
      <div className={styles.installRule} />
      <InstallSteps />
    </section>
  )
}

function Cost() {
  return (
    <section id="free" className={styles.section}>
      <h2 className={styles.heading}>Cost</h2>
      <p className={styles.cost}>
        Free and open source, no account. You use an API key from your own AI provider, and the provider bills you for what you use. Setup shows the expected daily use.
      </p>
    </section>
  )
}

function DataStorage() {
  return (
    <section className={`${styles.section} ${styles.storage}`}>
      <h2 className={`${styles.heading} ${styles.storageHeading}`}>Where your data is stored</h2>
      <ul className={styles.privacy}>
        {PRIVACY_LINES.map((line) => (
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
      <SectionLabel>Questions</SectionLabel>
      <Faq />
    </section>
  )
}

function Footer() {
  return (
    <footer className={styles.footer}>
      <div className={styles.footerRule} />
      <div className={styles.footerRow}>
        <span>jobranger · free and open source · MIT license</span>
        <span className={styles.footerLinks}>
          <a href={SOURCE_URL}>Source code</a>
          <Link to="/setup">Setup walkthrough</Link>
          <Link to="/app">Open the app</Link>
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
