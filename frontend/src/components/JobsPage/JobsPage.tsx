import { useState } from 'react'
import { Navigate, useParams, useSearchParams } from 'react-router'
import { describeCommandError } from '@/helpers/describeCommandError'
import { useAppBase } from '@/hooks/useAppBase'
import { useSnapshot } from '@/hooks/useSnapshot'
import { JobTabs } from '@/components/JobTabs/JobTabs'
import { JobsView } from '@/components/JobsView/JobsView'
import styles from '@/components/JobsLayout/JobsLayout.module.css'
import { listPath } from '@/helpers/adPaths'
import { DEFAULT_LIST, HIDDEN_KINDS, JOB_LISTS } from '@/helpers/adSelectors'
import { useAdMark } from '@/hooks/useAdMark'
import type { AdShowAnyway, HiddenKind } from '@/contract/payload.gen'
import { useCommand } from '@/hooks/useCommand'
import { useEditsDisabled } from '@/hooks/useEditsDisabled'
import { useText } from '@/hooks/useLanguage'
import { pickAllowed } from '@/helpers/pickAllowed'
import { StatusText } from '@/components/StatusText/StatusText'
import { usePageTitle } from '@/hooks/usePageTitle'

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
  const list = pickAllowed(JOB_LISTS, useParams().list)
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
        reason={pickAllowed(HIDDEN_KINDS, searchParams.get(REASON_PARAM))}
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
          <StatusText kind="error">{text.common.notSaved(describeCommandError(showAnyway.error, text))}</StatusText>
        </div>
      )}
    </>
  )
}
