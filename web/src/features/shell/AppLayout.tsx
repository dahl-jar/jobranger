import { useEffect, useEffectEvent, useState } from 'react'
import { Link, NavLink, Outlet, ScrollRestoration } from 'react-router'
import { jobsPath, settingsPath, useAppBase } from '@/app/paths'
import { canStoreScroll, scrollKey } from '@/app/scrollPositions'
import { ThemeSwitch } from '@/app/theme/ThemeSwitch'
import { useSnapshot } from '@/app/useSnapshot'
import { usePendingLinkHandoff } from '@/features/jobs/usePendingLinkHandoff'
import { RelayProvider } from '@/features/relay/RelayProvider'
import { RunLineControl, RunNowControl, RunStatus } from '@/features/run/RunControls'
import styles from '@/features/shell/AppLayout.module.css'
import { ConnectionGate } from '@/features/shell/ConnectionGate'
import { useLanguage, useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'

const SETTINGS_ICON_SIZE = 16

function useMachineLanguage(): void {
  const language = useSnapshot('config')?.language
  const { setLanguage } = useLanguage()
  const follow = useEffectEvent(setLanguage)

  useEffect(() => {
    if (language !== undefined) follow(language)
  }, [language])
}

export function AppShell() {
  const base = useAppBase()
  const text = useText()
  const [storesScroll] = useState(canStoreScroll)

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div className={styles.brand}>
          <Link to={jobsPath(base)} className={styles.logo}>
            jobranger
          </Link>
          <RunLineControl />
          <RunNowControl />
        </div>
        <div className={styles.tools}>
          <ThemeSwitch variant="labeled" />
          <NavLink to={settingsPath(base)} className={({ isActive }) => `${styles.settings} ${isActive ? styles.current : ''}`}>
            <Icon name="sliders" size={SETTINGS_ICON_SIZE} />
            {text.settings.nav}
          </NavLink>
        </div>
      </header>
      <div className={styles.doubleRule} />
      <RunStatus />
      <ConnectionGate>
        <Outlet />
      </ConnectionGate>
      {storesScroll && <ScrollRestoration getKey={scrollKey} />}
    </div>
  )
}

export function PairedShell() {
  usePendingLinkHandoff()
  useMachineLanguage()
  return <AppShell />
}

export function AppLayout() {
  return (
    <RelayProvider>
      <PairedShell />
    </RelayProvider>
  )
}
