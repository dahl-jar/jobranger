import styles from '@/features/setup/Terminal.module.css'
import { useText } from '@/shared/i18n/languageContext'

type TerminalLineKind = 'cmd' | 'out' | 'dim' | 'q' | 'sel' | 'ok' | 'part' | 'head' | 'big' | 'blank'

export type TerminalLine = readonly [TerminalLineKind, string]

const PREFIXES: Partial<Record<TerminalLineKind, string>> = { cmd: '$ ', q: '? ' }

export function Terminal({ command, lines }: { command: string; lines: readonly TerminalLine[] }) {
  return (
    <div className={styles.terminal}>
      <div className={styles.bar}>
        <span>~ terminal</span>
        <span>{command}</span>
      </div>
      <div role="log" aria-live="off" aria-label={useText().setup.terminalOutput} tabIndex={0} className={styles.output}>
        {lines.map(([kind, text], index) => (
          <div key={index} className={`${styles.line} ${styles[kind] ?? ''}`}>
            {PREFIXES[kind] !== undefined && <span className={styles.prefix}>{PREFIXES[kind]}</span>}
            {text}
          </div>
        ))}
        <div className={`${styles.line} ${styles.prompt}`}>
          $ <span aria-hidden="true" className={styles.cursor} />
        </div>
      </div>
    </div>
  )
}
