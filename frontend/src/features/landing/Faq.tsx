import { useId, useState } from 'react'
import styles from '@/features/landing/LandingPage.module.css'
import { useText } from '@/shared/i18n/languageContext'

const FIRST_OPEN_INDEX = 0

export function Faq() {
  const answerIdPrefix = useId()
  const items = useText().landing.faq
  const [openIndexes, setOpenIndexes] = useState<ReadonlySet<number>>(() => new Set([FIRST_OPEN_INDEX]))

  function toggle(index: number) {
    setOpenIndexes((previous) => {
      const next = new Set(previous)
      if (!next.delete(index)) next.add(index)
      return next
    })
  }

  return (
    <div>
      {items.map((item, index) => {
        const isOpen = openIndexes.has(index)
        const answerId = `${answerIdPrefix}-${index}`
        return (
          <div key={index} className={styles.faqItem}>
            <h3 className={styles.faqHeading}>
              <button type="button" className={styles.faqButton} aria-expanded={isOpen} aria-controls={answerId} onClick={() => toggle(index)}>
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
