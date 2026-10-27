import { useState } from 'react'
import { flushSync } from 'react-dom'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
import { AddApplicationForm, type ApplicationDraft } from '@/features/applications/AddApplicationForm'
import { APPLICATION_STATUSES, countStatuses, listedApplications, parseStatusFilter } from '@/features/applications/applicationStatus'
import { applicationPath, FROM_LIST_STATE } from '@/features/applications/applicationsPath'
import styles from '@/features/applications/Applications.module.css'
import { ApplicationsTable } from '@/features/applications/ApplicationsTable'
import { formatSort, nextSort, parseSort, sortApplications, type SortKey } from '@/features/applications/sortApplications'
import { ADD_BUTTON_ATTRIBUTE, rowOrAddButton } from '@/features/applications/rowFocus'
import type { RemoveWithUndo } from '@/features/applications/useRemoveWithUndo'
import type { ApplicationsSnapshot, ApplicationStatus } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'
import { Icon } from '@/shared/ui/Icon'
import { StatusText } from '@/shared/ui/StatusText'

const STATUS_PARAM = 'status'
const SORT_PARAM = 'sort'

interface ApplicationsPageProps {
  snapshot: Pick<ApplicationsSnapshot, 'applications' | 'applications_total'>
  today: string
  editsDisabled: boolean
  removal: Pick<RemoveWithUndo, 'pending' | 'undo'>
  onCreate: (draft: ApplicationDraft) => Promise<void>
  onStatusChange: (applicationId: number, status: ApplicationStatus) => void
}

export function ApplicationsPage({ snapshot, today, editsDisabled, removal, onCreate, onStatusChange }: ApplicationsPageProps) {
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const location = useLocation()
  const [adding, setAdding] = useState(false)
  const text = useText()
  const words = text.applications
  const filter = parseStatusFilter(searchParams.get(STATUS_PARAM))
  const sort = parseSort(searchParams.get(SORT_PARAM))
  const pending = removal.pending
  const listed = listedApplications(snapshot.applications, pending?.id)
  const counts = countStatuses(listed)
  const rows = sortApplications(
    listed.filter((application) => !filter || application.status === filter),
    sort,
  )
  const truncated = snapshot.applications_total > snapshot.applications.length

  function updateParam(name: string, value: string | undefined) {
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(name, value)
        else next.delete(name)
        return next
      },
      { replace: true },
    )
  }

  function undoRemoval(applicationId: number) {
    flushSync(removal.undo)
    rowOrAddButton(applicationId)?.focus()
  }

  function onSort(key: SortKey) {
    updateParam(SORT_PARAM, formatSort(nextSort(sort, key)))
  }

  async function add(draft: ApplicationDraft) {
    await onCreate(draft)
    updateParam(STATUS_PARAM, undefined)
  }

  return (
    <section aria-label={words.heading}>
      <div className={styles.toolbar}>
        <div className={styles.pills}>
          {APPLICATION_STATUSES.map((status) => (
            <button
              key={status}
              type="button"
              aria-pressed={filter === status}
              className={styles.pill}
              onClick={() => updateParam(STATUS_PARAM, filter === status ? undefined : status)}
            >
              {words.statuses[status]} {counts[status]}
            </button>
          ))}
        </div>
        <Button variant="quiet" size={40} className={styles.openAdd} {...{ [ADD_BUTTON_ATTRIBUTE]: '' }} onClick={() => setAdding(true)}>
          <Icon name="plus" />
          {words.addByHand}
        </Button>
      </div>
      {adding && <AddApplicationForm onAdd={add} onCancel={() => setAdding(false)} disabled={editsDisabled} />}
      <ApplicationsTable
        applications={rows}
        sort={sort}
        today={today}
        editsDisabled={editsDisabled}
        onSort={onSort}
        onOpen={(applicationId) => void navigate(applicationPath(applicationId, location.search), { state: FROM_LIST_STATE })}
        onStatusChange={onStatusChange}
      />
      {rows.length === 0 && <p className={styles.empty}>{words.empty}</p>}
      <p className={styles.footer}>
        {truncated && words.showingLatest(listed.length, snapshot.applications_total)}
        {words.clickRow}
      </p>
      <div className={styles.removal}>
        <StatusText kind={pending ? 'saved' : undefined}>
          {pending && (
            <>
              {words.removed(pending.company)}
              <Button variant="link" size={36} onClick={() => undoRemoval(pending.id)}>
                {text.common.undo}
              </Button>
            </>
          )}
        </StatusText>
      </div>
    </section>
  )
}
