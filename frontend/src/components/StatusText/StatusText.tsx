import type { ReactNode } from 'react'
import { Icon, type IconName } from '@/components/Icon/Icon'
import styles from '@/components/StatusText/StatusText.module.css'

type StatusKind = 'saved' | 'error'

const STATUS_ICONS: Record<StatusKind, IconName> = { saved: 'check', error: 'close' }

export function StatusText({ kind, children }: { kind?: StatusKind | undefined; children?: ReactNode }) {
  return (
    <p role="status" className={kind === undefined ? styles.status : `${styles.status} ${styles[kind]}`}>
      {kind !== undefined && (
        <>
          <Icon name={STATUS_ICONS[kind]} />
          {children}
        </>
      )}
    </p>
  )
}
