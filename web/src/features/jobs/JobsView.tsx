import type { AdCallbacks } from '@/features/jobs/AdActions'
import { AdList } from '@/features/jobs/AdList'
import { AllTable } from '@/features/jobs/AllTable'
import { ClosedTable } from '@/features/jobs/ClosedTable'
import { HiddenSummary } from '@/features/jobs/HiddenSummary'
import { HiddenTable } from '@/features/jobs/HiddenTable'
import styles from '@/features/jobs/JobsView.module.css'
import { LIST_NOTES } from '@/features/jobs/labels'
import { ListTabs } from '@/features/jobs/ListTabs'
import {
  hiddenBreakdown,
  type JobList,
  listCounts,
  selectActive,
  selectToday,
} from '@/features/jobs/selectors'
import type { AdsSnapshot, ConfigSnapshot, HiddenKind } from '@/features/relay/contract/payload.gen'

interface JobsViewProps extends AdCallbacks {
  list: JobList
  snapshot: AdsSnapshot
  config: ConfigSnapshot | undefined
  reason: HiddenKind | undefined
  movedIds: ReadonlySet<string>
  applications: number | undefined
  errors: Readonly<Record<string, string>>
  disabled: boolean
  onShowAnyway: (adId: string, on: boolean) => void
  onReasonChange: (reason: HiddenKind | undefined) => void
}

function ListBody(props: JobsViewProps) {
  const { list, snapshot, config, reason, errors, disabled } = props
  const bands = config?.targets.years
  switch (list) {
    case 'hidden':
      return (
        <HiddenTable
          snapshot={snapshot}
          reason={reason}
          bands={bands}
          movedIds={props.movedIds}
          disabled={disabled}
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
      return (
        <>
          <AdList
            ads={list === 'today' ? selectToday(snapshot) : selectActive(snapshot)}
            today={snapshot.as_of}
            roles={config?.roles ?? []}
            from={list}
            disabled={disabled}
            errors={errors}
            onMark={props.onMark}
            onUnmark={props.onUnmark}
          />
          {list === 'today' && <HiddenSummary breakdown={hiddenBreakdown(snapshot, bands)} />}
        </>
      )
  }
}

export function JobsView(props: JobsViewProps) {
  const note = LIST_NOTES[props.list]
  return (
    <>
      <ListTabs counts={listCounts(props.snapshot)} applications={props.applications} />
      {note !== undefined && <p className={styles.note}>{note}</p>}
      <ListBody {...props} />
    </>
  )
}
