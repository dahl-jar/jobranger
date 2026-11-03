import { type QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { DEMO_BASE } from '@/helpers/appPaths'
import { AppBaseContext } from '@/hooks/useAppBase'
import { createQueryClient } from '@/components/App/createQueryClient'
import { seedSnapshots } from '@/hooks/useSnapshot'
import { demoClock } from '@/components/DemoLayout/demoClock'
import styles from '@/components/DemoLayout/DemoLayout.module.css'
import { createDemoRelay } from '@/components/DemoLayout/demoRelay'
import { demoSnapshots } from '@/components/DemoLayout/demoSnapshots'
import { RelayContext } from '@/hooks/useRelay'
import { AppShell } from '@/components/AppLayout/AppLayout'
import { ClockContext } from '@/hooks/useClock'
import type { Messages } from '@/i18n/en'
import type { Language } from '@/helpers/languageChoice'
import { useLanguage, useText } from '@/hooks/useLanguage'

const SETUP_PATH = '/setup'

function seededClient(language: Language, text: Messages): QueryClient {
  const client = createQueryClient()
  seedSnapshots(client, demoSnapshots(language, text))
  return client
}

function DemoNote() {
  const words = useText().app.demo
  return (
    <div role="note" className={styles.note}>
      <div className={styles.row}>
        <span className={styles.text}>
          <span className={styles.label}>{words.label}</span> · {words.note}
        </span>
        <span className={styles.links}>
          <Link to="/" className={styles.home}>
            {words.backHome}
          </Link>
          <Link to={SETUP_PATH} className={styles.install}>
            {words.install}
          </Link>
        </span>
      </div>
    </div>
  )
}

function DemoApp({ language }: { language: Language }) {
  const text = useText()
  const [client] = useState(() => seededClient(language, text))
  const [clock] = useState(demoClock)
  const [relay] = useState(() => createDemoRelay(client, clock))
  return (
    <ClockContext value={clock}>
      <QueryClientProvider client={client}>
        <RelayContext value={relay}>
          <AppShell />
        </RelayContext>
      </QueryClientProvider>
    </ClockContext>
  )
}

export function DemoLayout() {
  const { language } = useLanguage()
  return (
    <AppBaseContext value={DEMO_BASE}>
      <DemoNote />
      <DemoApp key={language} language={language} />
    </AppBaseContext>
  )
}
