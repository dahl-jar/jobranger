import { type FormEvent, useEffect, useId, useRef, useState } from 'react'
import { Link } from 'react-router'
import { DEMO_BASE } from '@/app/paths'
import buttons from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import { PAIR_CODE_LENGTH } from '@/features/pairing/pairCode'
import styles from '@/features/pairing/PairPage.module.css'
import type { EndReason, FormError } from '@/features/pairing/pairingMachine'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'

const CODE_MAX_LENGTH = PAIR_CODE_LENGTH + 1

function formMessage(error: FormError, text: Messages): string {
  const errors = text.pairing.errors
  return error === 'invalid' ? errors.invalid(PAIR_CODE_LENGTH) : errors[error]
}

interface PairCodeFormProps {
  error: FormError | undefined
  ended: EndReason | undefined
  busy: boolean
  onSubmit: (code: string) => void
}

export function PairCodeForm({ error, ended, busy, onSubmit }: PairCodeFormProps) {
  const [code, setCode] = useState('')
  const input = useRef<HTMLInputElement>(null)
  const helpId = useId()
  const errorId = useId()
  const text = useText()
  const words = text.pairing
  const message = error === undefined ? ended && words.ended[ended] : formMessage(error, text)

  useEffect(() => {
    if (error !== undefined) input.current?.focus()
  }, [error])

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit(code)
  }

  return (
    <>
      <h1 tabIndex={-1} className={styles.title}>
        {words.titles.connect}
      </h1>
      <p className={styles.lead}>{words.form.lead}</p>
      <p className={styles.demo}>
        {words.form.justLooking}
        <Link to={DEMO_BASE}>{words.form.viewDemo}</Link>
      </p>
      <ol className={styles.steps}>
        <li className={styles.step}>
          <span className={styles.number}>1</span>
          <div className={styles.stepBody}>
            <h2 className={styles.stepTitle}>{words.form.runThis}</h2>
            <div className={styles.command}>
              <span className={styles.prompt}>$ </span>jobranger pair
            </div>
            <p className={styles.note}>
              {words.form.notInstalled}
              <Link to="/#install">{words.form.install}</Link>
              {words.form.or}
              <Link to="/setup">{words.form.seeSetup}</Link>.
            </p>
          </div>
        </li>
        <li className={styles.step}>
          <span className={styles.number}>2</span>
          <form className={styles.stepBody} onSubmit={submit} noValidate>
            <label htmlFor="pair-code" className={styles.stepTitle}>
              {words.form.enterCode}
            </label>
            <div className={styles.codeRow}>
              <input
                ref={input}
                id="pair-code"
                className={styles.codeInput}
                type="text"
                inputMode="text"
                autoComplete="one-time-code"
                autoCapitalize="characters"
                spellCheck={false}
                placeholder="XXXX-XXXX"
                maxLength={CODE_MAX_LENGTH}
                value={code}
                onChange={(event) => setCode(event.target.value)}
                aria-invalid={error !== undefined}
                aria-describedby={message === undefined ? helpId : `${errorId} ${helpId}`}
              />
              <button type="submit" className={`${buttons.primary} ${styles.large}`} disabled={busy}>
                {busy ? words.form.connectingBusy : words.form.connect}
              </button>
            </div>
            {message !== undefined && (
              <p id={errorId} role="alert" className={styles.error}>
                <Icon name="close" />
                {message}
              </p>
            )}
            <p id={helpId} className={styles.note}>
              {words.form.help(PAIR_CODE_LENGTH)}
            </p>
          </form>
        </li>
      </ol>
      <ul className={styles.privacy}>
        {words.privacy.map((line) => (
          <li key={line}>
            <span className={styles.ok}>
              <Icon name="check" />
            </span>
            {line}
          </li>
        ))}
      </ul>
    </>
  )
}
