import { useState } from 'react'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { useSnapshot } from '@/app/useSnapshot'
import { ApplicationsSummary } from '@/features/applications/ApplicationsSummary'
import { ConfigSummary } from '@/features/jobs/ConfigSummary'
import { RolesFilter } from '@/features/jobs/RolesFilter'
import styles from '@/features/jobs/JobsLayout.module.css'
import { machineToday } from '@/app/machineToday'
import { useConfigPatch } from '@/features/settings/useConfigPatch'
import { useEditsDisabled } from '@/features/shell/useEditsDisabled'
import { UsageSidebar } from '@/features/usage/UsageSidebar'
import { useText } from '@/shared/i18n/languageContext'
import { StatusText } from '@/shared/ui/StatusText'

export function JobsSidebar() {
  const ads = useSnapshot('ads')
  const applications = useSnapshot('applications')
  const usage = useSnapshot('usage')
  const config = useSnapshot('config')
  const editsDisabled = useEditsDisabled()
  const text = useText()
  const [failure, setFailure] = useState<string | undefined>(undefined)
  const { patch } = useConfigPatch({ onSaved: () => setFailure(undefined), onFailed: (error) => setFailure(commandErrorText(error, text)) })
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
