import { type FormEvent, useEffect, useId, useRef, useState } from 'react'
import { Link } from 'react-router'
import buttons from '@/shared/ui/button.module.css'
import { Icon } from '@/shared/ui/Icon'
import { PAIR_CODE_LENGTH } from '@/features/pairing/pairCode'
import styles from '@/features/pairing/PairPage.module.css'
import type { EndReason, FormError } from '@/features/pairing/pairingMachine'

const FORM_MESSAGES: Record<FormError, string> = {
  empty: 'Enter the code from your terminal.',
  invalid: `Check the code. It has ${PAIR_CODE_LENGTH} letters.`,
  wrong: "That code doesn't match. Check your terminal.",
  expired: 'This code has expired. Run jobranger pair again.',
  rateLimited: 'Too many tries. Wait a minute and try again.',
  unreachable: "Can't reach the relay. Try again.",
  storage: "This browser can't save the pairing. Allow site data and try again.",
}

const END_MESSAGES: Record<EndReason, string> = {
  refused: 'Pairing refused in the terminal.',
  timeout: 'Your terminal stopped waiting. Run jobranger pair again.',
  silent: "Your machine didn't answer. Run jobranger pair again.",
  cancelled: 'Pairing cancelled. Run jobranger pair again.',
  dropped: 'The connection dropped. Run jobranger pair again.',
  failed: 'Pairing failed. Run jobranger pair again.',
  revoked: 'Your machine unpaired this browser.',
  ticket: 'Pair this browser again.',
}

const CODE_MAX_LENGTH = PAIR_CODE_LENGTH + 1
const PRIVACY_LINES = [
  'Your data stays on your machine.',
  'This page sees only what your machine sends while open.',
  'Unpair with jobranger unpair.',
]

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
  const message = error === undefined ? ended && END_MESSAGES[ended] : FORM_MESSAGES[error]

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
        Connect your machine
      </h1>
      <p className={styles.lead}>Pair this page with jobranger on your computer. No account needed.</p>
      <ol className={styles.steps}>
        <li className={styles.step}>
          <span className={styles.number}>1</span>
          <div className={styles.stepBody}>
            <h2 className={styles.stepTitle}>Run this in your terminal</h2>
            <div className={styles.command}>
              <span className={styles.prompt}>$ </span>jobranger pair
            </div>
            <p className={styles.note}>
              Not installed yet? <Link to="/#install">Install jobranger</Link> or <Link to="/setup">see the setup</Link>.
            </p>
          </div>
        </li>
        <li className={styles.step}>
          <span className={styles.number}>2</span>
          <form className={styles.stepBody} onSubmit={submit} noValidate>
            <label htmlFor="pair-code" className={styles.stepTitle}>
              Enter the code it shows
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
                {busy ? 'Connecting...' : 'Connect'}
              </button>
            </div>
            {message !== undefined && (
              <p id={errorId} role="alert" className={styles.error}>
                <Icon name="close" />
                {message}
              </p>
            )}
            <p id={helpId} className={styles.note}>
              {PAIR_CODE_LENGTH} letters, valid for 10 minutes.
            </p>
          </form>
        </li>
      </ol>
      <ul className={styles.privacy}>
        {PRIVACY_LINES.map((line) => (
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
