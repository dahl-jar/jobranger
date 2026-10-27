import type { AdCallbacks } from '@/features/jobs/AdActions'
import { AdList } from '@/features/jobs/AdList'
import { AllTable } from '@/features/jobs/AllTable'
import { ClosedTable } from '@/features/jobs/ClosedTable'
import { HiddenSummary } from '@/features/jobs/HiddenSummary'
import { HiddenTable } from '@/features/jobs/HiddenTable'
import styles from '@/features/jobs/JobsView.module.css'
import {
  hiddenBreakdown,
  type JobList,
  selectActive,
  selectToday,
} from '@/features/jobs/selectors'
import type { AdsSnapshot, ConfigSnapshot, HiddenKind } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'

interface JobsViewProps extends AdCallbacks {
  list: JobList
  snapshot: AdsSnapshot
  config: ConfigSnapshot | undefined
  reason: HiddenKind | undefined
  movedIds: ReadonlySet<string>
  errors: Readonly<Record<string, string>>
  disabled: boolean
  onShowAnyway: (adId: string, on: boolean) => void
  onReasonChange: (reason: HiddenKind | undefined) => void
}

function MatchList(props: JobsViewProps & { list: 'today' | 'active' }) {
  const { list, snapshot, config } = props
  const text = useText()
  return (
    <>
      <AdList
        ads={list === 'today' ? selectToday(snapshot) : selectActive(snapshot)}
        today={snapshot.as_of}
        roles={config?.roles ?? []}
        from={list}
        disabled={props.disabled}
        errors={props.errors}
        onMark={props.onMark}
        onUnmark={props.onUnmark}
      />
      {list === 'today' && <HiddenSummary breakdown={hiddenBreakdown(snapshot, config?.targets.years, text)} />}
    </>
  )
}

function ListBody(props: JobsViewProps) {
  const { list, snapshot } = props
  switch (list) {
    case 'hidden':
      return (
        <HiddenTable
          snapshot={snapshot}
          reason={props.reason}
          bands={props.config?.targets.years}
          movedIds={props.movedIds}
          disabled={props.disabled}
          onReasonChange={props.onReasonChange}
          onShowAnyway={props.onShowAnyway}
        />
      )
    case 'closed':
      return <ClosedTable snapshot={snapshot} />
    case 'all':
      return <AllTable snapshot={snapshot} />
    case 'today':
    case 'active':
      return <MatchList {...props} list={list} />
  }
}

export function JobsView(props: JobsViewProps) {
  const notes: Partial<Record<JobList, string>> = useText().jobs.listNotes
  const note = notes[props.list]
  return (
    <>
      {note !== undefined && <p className={styles.note}>{note}</p>}
      <ListBody {...props} />
    </>
  )
}
