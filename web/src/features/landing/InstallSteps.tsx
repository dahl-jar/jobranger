import { Link } from 'react-router'
import { InstallTabs } from '@/features/landing/InstallTabs'
import styles from '@/features/landing/LandingPage.module.css'
import { INSTALL_STEPS } from '@/features/landing/landingContent'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'

const LINK_ICON_SIZE = 13

export function InstallSteps() {
  return (
    <>
      <div className={styles.row}>
        <div className={styles.number}>1</div>
        <div className={styles.stepBody}>
          <h3 className={styles.stepTitle}>Install the command line tool</h3>
          <InstallTabs />
        </div>
      </div>
      {INSTALL_STEPS.map((step) => (
        <div key={step.number} className={styles.row}>
          <div className={styles.number}>{step.number}</div>
          <div className={styles.stepBody}>
            <h3 className={styles.stepTitle}>{step.title}</h3>
            <div className={styles.stepCommand}>
              <span className={styles.prompt}>$ </span>
              {step.command}
            </div>
            <p className={styles.stepText}>{step.body}</p>
            <Link to={`/setup?step=${step.walkthroughStep}`} className={styles.stepLink}>
              See this step
              <Icon name="forward" size={LINK_ICON_SIZE} />
            </Link>
          </div>
        </div>
      ))}
      <div className={styles.row}>
        <div className={styles.number}>5</div>
        <div className={styles.stepBody}>
          <h3 className={styles.stepTitle}>Open the app</h3>
          <p className={styles.stepText}>
            Enter the code from <span className={styles.inlineCommand}>jobranger pair</span>. No account needed.
          </p>
          <Link to="/pair" className={`${button.primary} ${button.large} ${styles.connect}`}>
            Connect your machine
            <Icon name="forward" />
          </Link>
        </div>
      </div>
    </>
  )
}
