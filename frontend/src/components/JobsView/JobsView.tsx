import type { AdCallbacks } from '@/components/AdRow/AdActions'
import { AdList } from '@/components/JobsView/AdList'
import { AllAdsTable } from '@/components/JobsView/AllAdsTable'
import { ClosedAdsTable } from '@/components/JobsView/ClosedAdsTable'
import { HiddenSummary } from '@/components/JobsView/HiddenSummary'
import { HiddenAdsTable } from '@/components/JobsView/HiddenAdsTable'
import styles from '@/components/JobsView/JobsView.module.css'
import {
  hiddenBreakdown,
  type JobList,
  selectActive,
  selectToday,
} from '@/helpers/adSelectors'
import type { AdsSnapshot, ConfigSnapshot, HiddenKind } from '@/contract/payload.gen'
import { useText } from '@/hooks/useLanguage'

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
        <HiddenAdsTable
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
      return <ClosedAdsTable snapshot={snapshot} />
    case 'all':
      return <AllAdsTable snapshot={snapshot} />
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
