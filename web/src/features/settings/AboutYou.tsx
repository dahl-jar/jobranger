import { useId } from 'react'
import type { ContextSnapshot, ErrorMessage } from '@/features/relay/contract/payload.gen'
import styles from '@/features/settings/Settings.module.css'
import { useDraft } from '@/features/settings/useDraft'
import { CONTEXT_FULL } from '@/features/settings/uploadText'

const ABOUT_ROWS = 6

interface AboutYouProps {
  context: ContextSnapshot
  errorCode: ErrorMessage['code'] | undefined
  onSave: (about: string) => void
}

export function AboutYou({ context, errorCode, onSave }: AboutYouProps) {
  const fieldId = useId()
  const [draft, setDraft] = useDraft(context.about)
  const isOver = draft.length > context.about_limit

  function commit() {
    const trimmed = draft.trim()
    if (isOver || trimmed === context.about) return
    onSave(trimmed)
  }

  return (
    <section className={styles.group}>
      <label htmlFor={fieldId} className={styles.label}>
        About you
      </label>
      <div className={styles.rule} />
      <textarea
        id={fieldId}
        aria-describedby={`${fieldId}-note`}
        className={styles.textarea}
        rows={ABOUT_ROWS}
        placeholder="e.g. Built a booking API in C# with PostgreSQL and integration tests"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={commit}
      />
      <div className={styles.footer}>
        <p id={`${fieldId}-note`} className={styles.hint}>
          Projects, what you built, what your CV leaves out. The matcher can cite this.
        </p>
        <span className={isOver ? `${styles.counter} ${styles.over}` : styles.counter}>
          {draft.length} / {context.about_limit}
        </span>
      </div>
      {errorCode === 'context_full' && (
        <p role="alert" className={styles.error}>
          {CONTEXT_FULL}
        </p>
      )}
    </section>
  )
}
