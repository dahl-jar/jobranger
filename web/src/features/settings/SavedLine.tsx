import type { Recheck } from '@/features/relay/contract/payload.gen'
import { recheckText } from '@/features/settings/recheckText'
import styles from '@/features/settings/Settings.module.css'
import { useText } from '@/shared/i18n/languageContext'
import { StatusText } from '@/shared/ui/StatusText'

export type SaveResult = { kind: 'saved'; recheck: Recheck | undefined } | { kind: 'failed'; message: string }

export function SavedLine({ result }: { result: SaveResult | undefined }) {
  const text = useText()
  if (result === undefined) return <StatusText />
  if (result.kind === 'failed') return <StatusText kind="error">{text.common.notSaved(result.message)}</StatusText>
  const recheck = recheckText(result.recheck, text)
  return (
    <StatusText kind="saved">
      {text.common.saved}
      {recheck !== undefined && <span className={styles.recheck}>{recheck}</span>}
    </StatusText>
  )
}
