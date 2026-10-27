import { type RefObject, useContext, useEffect, useReducer, useRef, useState } from 'react'
import { useLocation } from 'react-router'
import buttons from '@/shared/ui/button.module.css'
import { usePageTitle } from '@/shared/usePageTitle'
import { PairCodeForm } from '@/features/pairing/PairCodeForm'
import { PairConnected } from '@/features/pairing/PairConnected'
import styles from '@/features/pairing/PairPage.module.css'
import { PairingDepsContext } from '@/features/pairing/pairingDeps'
import { defaultPairingDeps, PairingFlow } from '@/features/pairing/pairingFlow'
import { PairedSession } from '@/features/pairing/PairedSession'
import { type EndReason, pairingReducer, type PairingState } from '@/features/pairing/pairingMachine'
import { asRecord } from '@/shared/asRecord'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'

type TitleKey = keyof Messages['pairing']['titles']

const TITLES: Record<PairingState['name'], TitleKey> = {
  loading: 'connect',
  form: 'connect',
  claiming: 'connect',
  handshaking: 'connecting',
  confirming: 'check',
  waiting: 'waiting',
  connected: 'paired',
  paired: 'paired',
}

const ARRIVAL_REASONS: Record<string, EndReason> = { ticket: 'ticket', revoked: 'revoked', refused: 'refused' }

function arrivalReason(state: unknown): EndReason | undefined {
  const reason = asRecord(state)?.reason
  return typeof reason === 'string' && Object.hasOwn(ARRIVAL_REASONS, reason) ? ARRIVAL_REASONS[reason] : undefined
}

function Progress({ title, text }: { title: string; text: string }) {
  return (
    <>
      <h1 tabIndex={-1} className={styles.title}>
        {title}
      </h1>
      <p role="status" className={styles.lead}>
        {text}
      </p>
    </>
  )
}

function WordCompare({ words, onAnswer }: { words: string[]; onAnswer: (match: boolean) => void }) {
  const text = useText().pairing
  return (
    <>
      <h1 tabIndex={-1} className={styles.title}>
        {text.wordsTitle}
      </h1>
      <ol aria-label={text.wordsLabel} className={styles.words}>
        {words.map((word, index) => (
          <li key={`${index}-${word}`}>{word}</li>
        ))}
      </ol>
      <p className={styles.lead}>{text.answerInTerminal}</p>
      <div className={styles.actions}>
        <button type="button" className={buttons.primary} onClick={() => onAnswer(true)}>
          {text.wordsMatch}
        </button>
        <button type="button" className={buttons.outline} onClick={() => onAnswer(false)}>
          {text.wordsDiffer}
        </button>
      </div>
    </>
  )
}

const HEADING_SELECTOR = 'h1'

function viewOf(state: PairingState): PairingState['name'] {
  return state.name === 'claiming' ? 'form' : state.name
}

function useViewFocus(state: PairingState): RefObject<HTMLElement | null> {
  const main = useRef<HTMLElement>(null)
  const shown = useRef(viewOf(state))
  const view = viewOf(state)
  const focusInput = state.name === 'form' && state.error !== undefined

  useEffect(() => {
    const previous = shown.current
    shown.current = view
    if (previous === 'loading' || previous === view || focusInput) return
    main.current?.querySelector<HTMLElement>(HEADING_SELECTOR)?.focus()
  }, [view, focusInput])

  return main
}

export function PairPage() {
  const location = useLocation()
  const injected = useContext(PairingDepsContext)
  const [deps] = useState(() => injected ?? defaultPairingDeps())
  const [state, dispatch] = useReducer(pairingReducer, { name: 'loading' })
  const flow = useRef<PairingFlow | undefined>(undefined)
  const main = useViewFocus(state)
  const [arrival] = useState(() => arrivalReason(location.state))
  const pairing = useText().pairing
  usePageTitle(pairing.titles[TITLES[state.name]])

  useEffect(() => {
    const current = new PairingFlow(deps, dispatch)
    flow.current = current
    void current.load(arrival)
    return () => {
      void current.dispose()
    }
  }, [deps, arrival])

  return (
    <main ref={main} className={styles.page}>
      {(state.name === 'form' || state.name === 'claiming') && (
        <PairCodeForm
          error={state.name === 'form' ? state.error : undefined}
          ended={state.name === 'form' ? state.ended : undefined}
          busy={state.name === 'claiming'}
          onSubmit={(code) => void flow.current?.submit(code)}
        />
      )}
      {state.name === 'handshaking' && <Progress title={pairing.handshakeTitle} text={pairing.handshakeText} />}
      {state.name === 'confirming' && <WordCompare words={state.words} onAnswer={(match) => flow.current?.answer(match)} />}
      {state.name === 'waiting' && <Progress title={pairing.titles.waiting} text={pairing.waitingText} />}
      {state.name === 'connected' && <PairConnected status={state.status} onUnpair={() => void flow.current?.unpair()} />}
      {state.name === 'paired' && <PairedSession onEnded={(reason) => dispatch({ type: 'sessionEnded', reason })} />}
    </main>
  )
}
