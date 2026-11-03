import { useEffect, useEffectEvent, useState } from 'react'
import { Link, NavLink, Outlet, ScrollRestoration } from 'react-router'
import { jobsPath, settingsPath } from '@/helpers/appPaths'
import { useAppBase } from '@/hooks/useAppBase'
import { canStoreScroll, scrollKey } from '@/helpers/scrollPositions'
import { ThemeSwitch } from '@/components/ThemeSwitch/ThemeSwitch'
import { useSnapshot } from '@/hooks/useSnapshot'
import { usePendingLinkHandoff } from '@/components/AppLayout/usePendingLinkHandoff'
import { RelayProvider } from '@/components/RelayProvider/RelayProvider'
import { RunStatusLineControl, RunNowControl, RunStatus } from '@/components/RunControls/RunControls'
import styles from '@/components/AppLayout/AppLayout.module.css'
import { ConnectionGate } from '@/components/ConnectionGate/ConnectionGate'
import { useLanguage, useText } from '@/hooks/useLanguage'
import { Icon } from '@/components/Icon/Icon'

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
          <RunStatusLineControl />
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
