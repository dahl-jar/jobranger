import { useState } from 'react'
import { Navigate, useParams, useSearchParams } from 'react-router'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { useAppBase } from '@/app/paths'
import { useSnapshot } from '@/app/useSnapshot'
import { JobTabs } from '@/features/jobs/JobTabs'
import { JobsView } from '@/features/jobs/JobsView'
import styles from '@/features/jobs/JobsLayout.module.css'
import { listPath } from '@/features/jobs/paths'
import { DEFAULT_LIST, HIDDEN_KINDS, JOB_LISTS } from '@/features/jobs/selectors'
import { useAdMark } from '@/features/jobs/useAdMark'
import type { AdShowAnyway, HiddenKind } from '@/features/relay/contract/payload.gen'
import { useCommand } from '@/features/relay/useCommand'
import { useEditsDisabled } from '@/features/shell/useEditsDisabled'
import { useText } from '@/shared/i18n/languageContext'
import { oneOf } from '@/shared/oneOf'
import { StatusText } from '@/shared/ui/StatusText'
import { usePageTitle } from '@/shared/usePageTitle'

const REASON_PARAM = 'reason'

type ShowAnywayDraft = Omit<AdShowAnyway, 'id'>

function useShowAnyway() {
  const [movedIds, setMovedIds] = useState<ReadonlySet<string>>(new Set())

  function setMoved(adId: string, moved: boolean) {
    setMovedIds((current) => {
      const next = new Set(current)
      if (moved) next.add(adId)
      else next.delete(adId)
      return next
    })
  }

  const command = useCommand<ShowAnywayDraft>({
    onMutate: ({ ad_id: adId, on }) => setMoved(adId, on),
    onError: (_error, { ad_id: adId, on }) => setMoved(adId, !on),
  })
  return { movedIds, error: command.error, show: (adId: string, on: boolean) => command.mutate({ type: 'ad.show_anyway', v: 1, ad_id: adId, on }) }
}

export function JobsPage() {
  const text = useText()
  const base = useAppBase()
  const list = oneOf(JOB_LISTS, useParams().list)
  const [searchParams, setSearchParams] = useSearchParams()
  const ads = useSnapshot('ads')
  const config = useSnapshot('config')
  const editsDisabled = useEditsDisabled()
  const marks = useAdMark()
  const showAnyway = useShowAnyway()
  usePageTitle(list === undefined ? undefined : text.jobs.lists[list])

  if (list === undefined) return <Navigate to={listPath(DEFAULT_LIST, base)} replace />
  if (ads === undefined) return null

  function changeReason(reason: HiddenKind | undefined) {
    setSearchParams(reason === undefined ? {} : { [REASON_PARAM]: reason }, { replace: true })
  }

  return (
    <>
      <h1 className="visually-hidden">{text.jobs.lists[list]}</h1>
      <JobTabs />
      <JobsView
        list={list}
        snapshot={ads}
        config={config}
        reason={oneOf(HIDDEN_KINDS, searchParams.get(REASON_PARAM))}
        movedIds={showAnyway.movedIds}
        errors={marks.errors}
        disabled={editsDisabled}
        onMark={marks.mark}
        onUnmark={marks.unmark}
        onShowAnyway={showAnyway.show}
        onReasonChange={changeReason}
      />
      {showAnyway.error !== null && (
        <div className={styles.notice}>
          <StatusText kind="error">{text.common.notSaved(commandErrorText(showAnyway.error, text))}</StatusText>
        </div>
      )}
    </>
  )
}
