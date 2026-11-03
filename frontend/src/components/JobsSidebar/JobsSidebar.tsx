import { useState } from 'react'
import { describeCommandError } from '@/helpers/describeCommandError'
import { useSnapshot } from '@/hooks/useSnapshot'
import { ApplicationsSummary } from '@/components/ApplicationsSummary/ApplicationsSummary'
import { ConfigSummary } from '@/components/JobsSidebar/ConfigSummary'
import { RolesFilter } from '@/components/JobsSidebar/RolesFilter'
import styles from '@/components/JobsLayout/JobsLayout.module.css'
import { machineToday } from '@/helpers/machineToday'
import { useConfigPatch } from '@/hooks/useConfigPatch'
import { useEditsDisabled } from '@/hooks/useEditsDisabled'
import { UsageSidebar } from '@/components/UsageSidebar/UsageSidebar'
import { useText } from '@/hooks/useLanguage'
import { StatusText } from '@/components/StatusText/StatusText'

export function JobsSidebar() {
  const ads = useSnapshot('ads')
  const applications = useSnapshot('applications')
  const usage = useSnapshot('usage')
  const config = useSnapshot('config')
  const editsDisabled = useEditsDisabled()
  const text = useText()
  const [failure, setFailure] = useState<string | undefined>(undefined)
  const { patch } = useConfigPatch({ onSaved: () => setFailure(undefined), onFailed: (error) => setFailure(describeCommandError(error, text)) })
  if (ads === undefined) return null
  return (
    <aside aria-label={text.jobs.sidebar} className={styles.aside}>
      {applications !== undefined && (
        <ApplicationsSummary applications={applications.applications} total={applications.applications_total} today={ads.as_of} />
      )}
      {usage !== undefined && (
        <div className={styles.block}>
          <UsageSidebar usage={usage} today={machineToday(ads, usage)} monthlyLimitUsd={config?.model.monthly_limit_usd ?? null} />
        </div>
      )}
      {config !== undefined && (
        <>
          <RolesFilter roles={config.roles} picked={config.targets.subfields} disabled={editsDisabled} onChange={(subfields) => patch({ targets: { subfields } })} />
          {failure !== undefined && <StatusText kind="error">{text.common.notSaved(failure)}</StatusText>}
          <ConfigSummary targets={config.targets} />
        </>
      )}
    </aside>
  )
}
