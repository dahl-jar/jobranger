import type { Recheck } from '@/features/relay/contract/payload.gen'
import { recheckText } from '@/features/settings/recheckText'
import styles from '@/features/settings/Settings.module.css'
import { StatusText } from '@/shared/ui/StatusText'

export type SaveResult = { kind: 'saved'; recheck: Recheck | undefined } | { kind: 'failed'; message: string }

export function SavedLine({ result }: { result: SaveResult | undefined }) {
  if (result === undefined) return <StatusText />
  if (result.kind === 'failed') return <StatusText kind="error">Not saved: {result.message}</StatusText>
  const recheck = recheckText(result.recheck)
  return (
    <StatusText kind="saved">
      Saved
      {recheck !== undefined && <span className={styles.recheck}>{recheck}</span>}
    </StatusText>
  )
}
