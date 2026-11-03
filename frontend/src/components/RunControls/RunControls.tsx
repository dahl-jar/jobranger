import { useQueryClient } from '@tanstack/react-query'
import { describeCommandError } from '@/helpers/describeCommandError'
import { QUERY_KEYS } from '@/constants/queryKeys'
import { useSnapshot } from '@/hooks/useSnapshot'
import type { CommandError } from '@/services/relayCommands/commandTracker'
import type { RunCancel, RunStart } from '@/contract/payload.gen'
import { useConnection } from '@/hooks/useRelay'
import { useCommand } from '@/hooks/useCommand'
import styles from '@/components/RunControls/RunControls.module.css'
import { RunFinishedBanner } from '@/components/RunControls/RunFinishedBanner'
import { RunStatusLine } from '@/components/RunControls/RunStatusLine'
import { RunNowButton } from '@/components/RunControls/RunNowButton'
import { RunProgress } from '@/components/RunControls/RunProgress'
import { runCounts, runTitle, type RunView } from '@/helpers/runProgressView'
import { useEditsDisabled } from '@/hooks/useEditsDisabled'
import { formatClock } from '@/helpers/formatDate'
import type { Messages } from '@/i18n/en'
import { useText } from '@/hooks/useLanguage'

type RunStartDraft = Omit<RunStart, 'id'>
type RunCancelDraft = Omit<RunCancel, 'id'>

const RUN_START: RunStartDraft = { type: 'run.start', v: 1 }

function announcement(view: RunView | undefined, text: Messages): string {
  if (view === undefined) return ''
  switch (view.kind) {
    case 'running':
      return `${runTitle(view.progress, text)} ${text.numbers.percent(view.progress.percent)}`
    case 'finished':
      return `${text.run.finishedAt(formatClock(view.result.finished_at))}. ${runCounts(view.result, text)}`
    case 'cancelled':
      return text.run.cancelled
  }
}

function startFailure(error: CommandError | null, resultsBy: string | null, text: Messages): string | undefined {
  if (error === null) return undefined
  if (error.code !== 'run_in_progress') return text.common.notSaved(describeCommandError(error, text))
  return resultsBy === null ? text.run.alreadyRunning : `${text.run.titles.scheduled(formatClock(resultsBy))}.`
}

export function RunStatusLineControl() {
  const status = useSnapshot('status')
  const offline = useConnection().kind === 'machineOffline'
  return <RunStatusLine status={status} offline={offline} />
}

export function RunNowControl() {
  const text = useText()
  const status = useSnapshot('status')
  const editsDisabled = useEditsDisabled()
  const start = useCommand<RunStartDraft>()
  const reason = startFailure(start.error, status?.running?.results_by ?? null, text)
  return (
    <>
      <RunNowButton running={(status?.running ?? null) !== null} editsDisabled={editsDisabled} pending={start.isPending} onRun={() => start.mutate(RUN_START)} />
      {reason && (
        <span role="alert" className={`${styles.line} ${styles.offline}`}>
          {reason}
        </span>
      )}
    </>
  )
}

export function RunStatus() {
  const text = useText()
  const view = useSnapshot('run')
  const queryClient = useQueryClient()
  const editsDisabled = useEditsDisabled()
  const cancel = useCommand<RunCancelDraft>()
  const dismiss = () => void queryClient.resetQueries({ queryKey: QUERY_KEYS.run })
  return (
    <>
      <p role="status" className="visually-hidden">
        {announcement(view, text)}
      </p>
      {view?.kind === 'running' && (
        <RunProgress
          progress={view.progress}
          editsDisabled={editsDisabled}
          pending={cancel.isPending}
          onCancel={(runId) => cancel.mutate({ type: 'run.cancel', v: 1, run_id: runId })}
        />
      )}
      {view !== undefined && view.kind !== 'running' && <RunFinishedBanner view={view} onDismiss={dismiss} />}
    </>
  )
}
