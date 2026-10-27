import { useEffect, useRef } from 'react'
import { Link, useSearchParams } from 'react-router'
import styles from '@/features/setup/SetupPage.module.css'
import { setupSteps, type SetupStep } from '@/features/setup/setupSteps'
import { Terminal } from '@/features/setup/Terminal'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'
import { usePageTitle } from '@/shared/usePageTitle'

const STEP_PARAM = 'step'
const PAIR_PATH = '/pair'

function pickStep(steps: readonly [SetupStep, ...SetupStep[]], requested: string | null): { step: SetupStep; number: number } {
  const parsed = Number.parseInt(requested ?? '', 10)
  const number = Number.isNaN(parsed) ? 1 : Math.min(Math.max(parsed, 1), steps.length)
  return { step: steps[number - 1] ?? steps[0], number }
}

export function SetupPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const text = useText()
  const words = text.setup
  const steps = setupSteps(`${window.location.origin}${PAIR_PATH}`, text)
  const { step, number } = pickStep(steps, searchParams.get(STEP_PARAM))
  const nextStep = steps[number]
  const goTo = (target: number) => setSearchParams({ [STEP_PARAM]: String(target) }, { preventScrollReset: true })
  const headingRef = useRef<HTMLHeadingElement>(null)
  const shownNumber = useRef(number)

  usePageTitle(words.pageTitle(number, steps.length))

  useEffect(() => {
    if (shownNumber.current === number) return
    shownNumber.current = number
    headingRef.current?.focus({ preventScroll: true })
    headingRef.current?.scrollIntoView({ block: 'nearest' })
  }, [number])

  return (
    <div className={styles.layout}>
      <nav aria-label={words.stepsNav} className={styles.steps}>
        <div className={styles.stepsLabel}>{words.stepsLabel}</div>
        <ol className={styles.stepList}>
          {steps.map((item, index) => {
            const itemNumber = index + 1
            const isDone = itemNumber < number
            return (
              <li key={item.id} className={styles.stepItem}>
                <Link
                  to={`?${STEP_PARAM}=${itemNumber}`}
                  preventScrollReset
                  aria-current={itemNumber === number ? 'step' : undefined}
                  className={`${styles.stepLink} ${isDone ? styles.done : ''}`}
                >
                  <span className={styles.stepNumber}>{itemNumber}</span>
                  <span className={styles.stepTitle}>{item.title}</span>
                  {isDone && (
                    <>
                      <span className={styles.check}>
                        <Icon name="check" />
                      </span>
                      <span className="visually-hidden">{words.done}</span>
                    </>
                  )}
                </Link>
              </li>
            )
          })}
        </ol>
        <p className={styles.caption}>{words.caption}</p>
      </nav>
      <main className={styles.main}>
        <div className={styles.intro}>
          <div className={styles.counter}>{words.counter(number, steps.length)}</div>
          <h1 ref={headingRef} tabIndex={-1} className={styles.title}>
            {step.title}
          </h1>
          <p className={styles.body}>{step.body}</p>
        </div>
        <Terminal command={step.command} lines={step.lines} />
        <div className={styles.controls}>
          <Button variant="quiet" className={button.compact} disabled={number === 1} onClick={() => goTo(number - 1)}>
            <Icon name="back" />
            {words.back}
          </Button>
          {nextStep === undefined ? (
            <Link to={PAIR_PATH} className={`${button.primary} ${button.compact} ${button.roomy}`}>
              {words.connect}
              <Icon name="forward" />
            </Link>
          ) : (
            <Button className={`${button.compact} ${button.roomy}`} onClick={() => goTo(number + 1)}>
              {words.next(nextStep.title)}
              <Icon name="forward" />
            </Button>
          )}
        </div>
        {step.id === 'pair' && (
          <p className={styles.pairHint}>
            {words.pairHintBefore}
            <Link to={PAIR_PATH}>{words.pairHintLink}</Link>.
          </p>
        )}
      </main>
    </div>
  )
}
