import { Link } from 'react-router'
import { InstallTabs } from '@/features/landing/InstallTabs'
import styles from '@/features/landing/LandingPage.module.css'
import { INSTALL_STEPS } from '@/features/landing/landingContent'
import { useText } from '@/shared/i18n/languageContext'
import button from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'

const LINK_ICON_SIZE = 13

export function InstallSteps() {
  const words = useText().landing

  return (
    <>
      <div className={styles.row}>
        <div className={styles.number}>1</div>
        <div className={styles.stepBody}>
          <h3 className={styles.stepTitle}>{words.installTool}</h3>
          <InstallTabs />
        </div>
      </div>
      {INSTALL_STEPS.map((step) => (
        <div key={step.number} className={styles.row}>
          <div className={styles.number}>{step.number}</div>
          <div className={styles.stepBody}>
            <h3 className={styles.stepTitle}>{words.installSteps[step.id].title}</h3>
            <div className={styles.stepCommand}>
              <span className={styles.prompt}>$ </span>
              {step.command}
            </div>
            <p className={styles.stepText}>{words.installSteps[step.id].body}</p>
            <Link to={`/setup?step=${step.walkthroughStep}`} className={styles.stepLink}>
              {words.seeStep}
              <Icon name="forward" size={LINK_ICON_SIZE} />
            </Link>
          </div>
        </div>
      ))}
      <div className={styles.row}>
        <div className={styles.number}>5</div>
        <div className={styles.stepBody}>
          <h3 className={styles.stepTitle}>{words.openAppStep}</h3>
          <p className={styles.stepText}>
            {words.enterCodeBefore}
            <span className={styles.inlineCommand}>jobranger pair</span>
            {words.enterCodeAfter}
          </p>
          <Link to="/pair" className={`${button.primary} ${button.large} ${styles.connect}`}>
            {words.connect}
            <Icon name="forward" />
          </Link>
        </div>
      </div>
    </>
  )
}
