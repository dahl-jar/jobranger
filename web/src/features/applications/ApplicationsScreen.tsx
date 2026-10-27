import { useState } from 'react'
import { Navigate, useLocation, useParams } from 'react-router'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { useAppBase } from '@/app/paths'
import { QUERY_KEYS } from '@/app/queryKeys'
import { useOptimisticWrite } from '@/app/useOptimisticWrite'
import { useSnapshot } from '@/app/useSnapshot'
import type { ApplicationDraft } from '@/features/applications/AddApplicationForm'
import { ApplicationPanel } from '@/features/applications/ApplicationPanel'
import { ApplicationsPage } from '@/features/applications/ApplicationsPage'
import { applicationsPath } from '@/features/applications/applicationsPath'
import { useRemoveWithUndo } from '@/features/applications/useRemoveWithUndo'
import styles from '@/features/jobs/JobsLayout.module.css'
import { JobTabs } from '@/features/jobs/JobTabs'
import type {
  Application,
  ApplicationCreate,
  ApplicationRemove,
  ApplicationsSnapshot,
  ApplicationStatus,
  ApplicationUpdate,
} from '@/features/relay/contract/payload.gen'
import { useCommand } from '@/features/relay/useCommand'
import { useEditsDisabled } from '@/features/shell/useEditsDisabled'
import { useText } from '@/shared/i18n/languageContext'
import { usePageTitle } from '@/shared/usePageTitle'

type CreateDraft = Omit<ApplicationCreate, 'id'>
type UpdateDraft = Omit<ApplicationUpdate, 'id'>
type RemoveDraft = Omit<ApplicationRemove, 'id'>
type ChangeDraft = UpdateDraft | RemoveDraft
type ApplicationChange = Partial<Pick<Application, 'status' | 'note' | 'removed'>>

function findApplication(snapshot: ApplicationsSnapshot, applicationId: number): Application | undefined {
  return snapshot.applications.find((application) => application.id === applicationId)
}

function withChange(snapshot: ApplicationsSnapshot, applicationId: number, change: ApplicationChange): ApplicationsSnapshot {
  return {
    ...snapshot,
    applications: snapshot.applications.map((application) => (application.id === applicationId ? { ...application, ...change } : application)),
  }
}

function pick(application: Application | undefined, change: ApplicationChange): ApplicationChange {
  if (application === undefined) return {}
  return Object.fromEntries(Object.keys(change).map((key) => [key, application[key as keyof ApplicationChange]]))
}

function changeOf(draft: ChangeDraft): ApplicationChange {
  return draft.type === 'application.remove' ? { removed: draft.removed } : { ...(draft.status && { status: draft.status }), ...(draft.note !== undefined && { note: draft.note }) }
}

function stillWritten(snapshot: ApplicationsSnapshot, applicationId: number, written: ApplicationChange): boolean {
  const current = pick(findApplication(snapshot, applicationId), written)
  return Object.entries(written).every(([key, value]) => current[key as keyof ApplicationChange] === value)
}

function useApplicationChange(onFailed: (message: string) => void) {
  const text = useText()
  const command = useOptimisticWrite<ApplicationsSnapshot, ChangeDraft>({
    queryKey: QUERY_KEYS.applications,
    apply: (snapshot, draft) => withChange(snapshot, draft.application_id, changeOf(draft)),
    holds: (current, draft) => stillWritten(current, draft.application_id, changeOf(draft)),
    restore: (current, draft, previous) => withChange(current, draft.application_id, pick(findApplication(previous, draft.application_id), changeOf(draft))),
    onError: (error) => onFailed(commandErrorText(error, text)),
  })
  return command.mutate
}

export function ApplicationsScreen() {
  const text = useText()
  const base = useAppBase()
  const { search } = useLocation()
  const { applicationId } = useParams()
  const snapshot = useSnapshot('applications')
  const ads = useSnapshot('ads')
  const config = useSnapshot('config')
  const editsDisabled = useEditsDisabled()
  const [failure, setFailure] = useState<string | undefined>(undefined)
  const change = useApplicationChange(setFailure)
  const create = useCommand<CreateDraft>()
  const removal = useRemoveWithUndo((id) => change({ type: 'application.remove', v: 1, application_id: id, removed: true }))
  usePageTitle(text.applications.heading)
  if (snapshot === undefined || ads === undefined) return null

  const opened = applicationId === undefined ? undefined : snapshot.applications.find((application) => String(application.id) === applicationId && !application.removed)
  if (applicationId !== undefined && opened === undefined) return <Navigate to={`${applicationsPath(base)}${search}`} replace />

  async function createApplication(draft: ApplicationDraft) {
    try {
      await create.mutateAsync({ type: 'application.create', v: 1, ...draft })
    } catch (error: unknown) {
      throw new Error(commandErrorText(error, text), { cause: error })
    }
  }

  function changeStatus(id: number, status: ApplicationStatus) {
    setFailure(undefined)
    change({ type: 'application.update', v: 1, application_id: id, status })
  }

  function changeNote(id: number, note: string) {
    setFailure(undefined)
    change({ type: 'application.update', v: 1, application_id: id, note })
  }

  return (
    <>
      <h1 className="visually-hidden">{text.applications.heading}</h1>
      <JobTabs />
      <ApplicationsPage
        snapshot={snapshot}
        today={ads.as_of}
        editsDisabled={editsDisabled}
        removal={removal}
        onCreate={createApplication}
        onStatusChange={changeStatus}
      />
      {failure !== undefined && (
        <p role="alert" className={styles.error}>
          {text.common.notSaved(failure)}
        </p>
      )}
      {opened !== undefined && (
        <ApplicationPanel
          application={opened}
          today={ads.as_of}
          roles={config?.roles ?? []}
          editsDisabled={editsDisabled}
          onStatusChange={changeStatus}
          onNoteChange={changeNote}
          onRemove={removal.remove}
        />
      )}
    </>
  )
}
