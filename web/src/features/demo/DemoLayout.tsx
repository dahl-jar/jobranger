import { type QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { AppBaseContext, DEMO_BASE } from '@/app/paths'
import { createQueryClient } from '@/app/queryClient'
import { seedSnapshots } from '@/app/useSnapshot'
import { demoClock } from '@/features/demo/demoClock'
import styles from '@/features/demo/DemoLayout.module.css'
import { createDemoRelay } from '@/features/demo/demoRelay'
import { demoSnapshots } from '@/features/demo/demoSnapshots'
import { RelayContext } from '@/features/relay/relayContext'
import { AppShell } from '@/features/shell/AppLayout'
import { ClockContext } from '@/shared/clock'
import type { Messages } from '@/shared/i18n/en'
import type { Language } from '@/shared/i18n/language'
import { useLanguage, useText } from '@/shared/i18n/languageContext'

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
