import { useLocation } from 'react-router'
import { useSnapshot } from '@/hooks/useSnapshot'
import { AdDetail } from '@/components/AdDetailPage/AdDetail'
import styles from '@/components/AdRow/AdRow.module.css'
import { type DeepLink, parseAdSearch } from '@/helpers/adDeepLink'
import { JobTabs } from '@/components/JobTabs/JobTabs'
import { useAdMark } from '@/hooks/useAdMark'
import { useEmailLinkAction } from '@/components/AdDetailPage/useEmailLinkAction'
import { useEditsDisabled } from '@/hooks/useEditsDisabled'
import { useText } from '@/hooks/useLanguage'
import { Button } from '@/components/Button/Button'
import { StatusText } from '@/components/StatusText/StatusText'
import { usePageTitle } from '@/hooks/usePageTitle'

interface LinkNoticeProps {
  choice: DeepLink | undefined
  notice: DeepLink | undefined
  disabled: boolean
  onConfirm: () => void
  onUndo: (adId: string) => void
}

function LinkNotice({ choice, notice, disabled, onConfirm, onUndo }: LinkNoticeProps) {
  const text = useText()
  if (choice !== undefined) {
    return (
      <p className={styles.linkChoice}>
        {text.jobs.deepLink.fromEmail}
        <Button size={36} disabled={disabled} onClick={onConfirm}>
          {text.jobs.deepLink.markAs[choice.action]}
        </Button>
      </p>
    )
  }
  return (
    <StatusText kind={notice && 'saved'}>
      {notice && (
        <>
          {text.jobs.deepLink[notice.action]}
          <Button variant="link" size={36} disabled={disabled} onClick={() => onUndo(notice.adId)}>
            {text.common.undo}
          </Button>
        </>
      )}
    </StatusText>
  )
}

export function AdDetailPage() {
  const text = useText()
  const location = useLocation()
  const ads = useSnapshot('ads')
  const config = useSnapshot('config')
  const editsDisabled = useEditsDisabled()
  const marks = useAdMark()
  const { choice, confirmChoice, notice, clearNotice } = useEmailLinkAction({ ads, mark: marks.mark })
  const adId = parseAdSearch(location.search)?.adId
  const ad = ads?.active.find((candidate) => candidate.id === adId)
  usePageTitle(ads === undefined ? undefined : (ad?.title ?? text.jobs.goneTitle))
  if (ads === undefined) return null

  return (
    <>
      <JobTabs />
      <AdDetail
        ad={ad}
        today={ads.as_of}
        roles={config?.roles ?? []}
        from={Reflect.get(Object(location.state), 'from')}
        disabled={editsDisabled}
        error={adId === undefined ? undefined : marks.errors[adId]}
        notice={
          <LinkNotice
            choice={choice?.adId === adId ? choice : undefined}
            notice={notice !== undefined && ad?.mark === notice.action ? notice : undefined}
            disabled={editsDisabled}
            onConfirm={confirmChoice}
            onUndo={(undoneId) => {
              clearNotice()
              marks.unmark(undoneId)
            }}
          />
        }
        onMark={marks.mark}
        onUnmark={marks.unmark}
      />
    </>
  )
}
