import type { Recheck } from '@/contract/payload.gen'
import { describeRecheck } from '@/components/SettingsPage/describeRecheck'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { useText } from '@/hooks/useLanguage'
import { StatusText } from '@/components/StatusText/StatusText'

export type SaveResult = { kind: 'saved'; recheck: Recheck | undefined } | { kind: 'failed'; message: string }

export function SaveResultLine({ result }: { result: SaveResult | undefined }) {
  const text = useText()
  if (result === undefined) return <StatusText />
  if (result.kind === 'failed') return <StatusText kind="error">{text.common.notSaved(result.message)}</StatusText>
  const recheck = describeRecheck(result.recheck, text)
  return (
    <StatusText kind="saved">
      {text.common.saved}
      {recheck !== undefined && <span className={styles.recheck}>{recheck}</span>}
    </StatusText>
  )
}
