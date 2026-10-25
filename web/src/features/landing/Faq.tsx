import { useId, useState } from 'react'
import styles from '@/features/landing/LandingPage.module.css'
import { FAQ_ITEMS } from '@/features/landing/landingContent'

export function Faq() {
  const answerIdPrefix = useId()
  const [openQuestions, setOpenQuestions] = useState<ReadonlySet<string>>(() => new Set([FAQ_ITEMS[0].question]))

  function toggle(question: string) {
    setOpenQuestions((previous) => {
      const next = new Set(previous)
      if (!next.delete(question)) next.add(question)
      return next
    })
  }

  return (
    <div>
      {FAQ_ITEMS.map((item, index) => {
        const isOpen = openQuestions.has(item.question)
        const answerId = `${answerIdPrefix}-${index}`
        return (
          <div key={item.question} className={styles.faqItem}>
            <h3 className={styles.faqHeading}>
              <button type="button" className={styles.faqButton} aria-expanded={isOpen} aria-controls={answerId} onClick={() => toggle(item.question)}>
                {item.question}
                <span aria-hidden="true" className={styles.faqSign}>
                  {isOpen ? '−' : '+'}
                </span>
              </button>
            </h3>
            <p id={answerId} hidden={!isOpen} className={styles.faqAnswer}>
              {item.answer}
            </p>
          </div>
        )
      })}
    </div>
  )
}
