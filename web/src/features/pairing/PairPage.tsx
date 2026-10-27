import { type RefObject, useContext, useEffect, useReducer, useRef, useState } from 'react'
import buttons from '@/shared/ui/button.module.css'
import { usePageTitle } from '@/shared/usePageTitle'
import { PairCodeForm } from '@/features/pairing/PairCodeForm'
import { PairConnected } from '@/features/pairing/PairConnected'
import styles from '@/features/pairing/PairPage.module.css'
import { PairingDepsContext } from '@/features/pairing/pairingDeps'
import { defaultPairingDeps, PairingFlow } from '@/features/pairing/pairingFlow'
import { pairingReducer, type PairingState } from '@/features/pairing/pairingMachine'

const TITLES: Record<PairingState['name'], string> = {
  loading: 'Connect your machine',
  form: 'Connect your machine',
  claiming: 'Connect your machine',
  handshaking: 'Connecting',
  confirming: 'Check the words',
  waiting: 'Waiting for your terminal',
  connected: 'Paired',
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
  return (
    <>
      <h1 tabIndex={-1} className={styles.title}>
        Do these words match your terminal?
      </h1>
      <ol aria-label="Confirmation words" className={styles.words}>
        {words.map((word, index) => (
          <li key={`${index}-${word}`}>{word}</li>
        ))}
      </ol>
      <p className={styles.lead}>Answer in your terminal too.</p>
      <div className={styles.actions}>
        <button type="button" className={buttons.primary} onClick={() => onAnswer(true)}>
          Yes, they match
        </button>
        <button type="button" className={buttons.outline} onClick={() => onAnswer(false)}>
          No
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
  const injected = useContext(PairingDepsContext)
  const [deps] = useState(() => injected ?? defaultPairingDeps())
  const [state, dispatch] = useReducer(pairingReducer, { name: 'loading' })
  const flow = useRef<PairingFlow | undefined>(undefined)
  const main = useViewFocus(state)
  usePageTitle(TITLES[state.name])

  useEffect(() => {
    const current = new PairingFlow(deps, dispatch)
    flow.current = current
    void current.load()
    return () => {
      void current.dispose()
    }
  }, [deps])

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
      {state.name === 'handshaking' && <Progress title="Connecting to your machine" text="Keep jobranger pair running in your terminal." />}
      {state.name === 'confirming' && <WordCompare words={state.words} onAnswer={(match) => flow.current?.answer(match)} />}
      {state.name === 'waiting' && <Progress title="Waiting for your terminal" text="Answer in your terminal to finish pairing." />}
      {state.name === 'connected' && <PairConnected status={state.status} onUnpair={() => void flow.current?.unpair()} />}
    </main>
  )
}
