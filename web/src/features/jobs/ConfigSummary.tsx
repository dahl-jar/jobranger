import { Link } from 'react-router'
import { settingsPath, useAppBase } from '@/app/paths'
import { summarizeTargets } from '@/features/jobs/summarizeTargets'
import { DEFAULT_SECTION } from '@/features/settings/settingsSections'
import styles from '@/features/jobs/JobsView.module.css'
import type { Targets } from '@/features/relay/contract/payload.gen'
import { useText } from '@/shared/i18n/languageContext'
import { Icon, type IconName } from '@/shared/ui/Icon'
import { SectionLabel } from '@/shared/ui/SectionLabel'

const FACT_ICON_SIZE = 18

function Fact({ icon, label, value }: { icon: IconName; label: string; value: string }) {
  return (
    <div className={styles.fact}>
      <span className={styles.factIcon}>
        <Icon name={icon} size={FACT_ICON_SIZE} />
      </span>
      <div>
        <div className={styles.factLabel}>{label}</div>
        <div className={styles.factValue}>{value}</div>
      </div>
    </div>
  )
}

export function ConfigSummary({ targets }: { targets: Targets }) {
  const text = useText()
  const base = useAppBase()
  const words = text.jobs.config
  const summary = summarizeTargets(targets, text)
  return (
    <section className={styles.block}>
      <SectionLabel
        aside={
          <Link to={settingsPath(base, DEFAULT_SECTION)} aria-label={words.editLabel} className={styles.edit}>
            <Icon name="pencil" />
            {text.common.edit}
          </Link>
        }
      >
        {words.heading}
      </SectionLabel>
      <div className={styles.facts}>
        <Fact icon="level" label={words.level} value={summary.level} />
        <Fact icon="clock" label={words.years} value={summary.years} />
        <Fact icon="cap" label={words.masters} value={summary.masters} />
        <Fact icon="place" label={words.places} value={summary.places} />
      </div>
    </section>
  )
}
