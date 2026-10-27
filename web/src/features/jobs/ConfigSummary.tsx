import { Link } from 'react-router'
import { summarizeTargets } from '@/features/jobs/summarizeTargets'
import styles from '@/features/jobs/JobsView.module.css'
import type { Targets } from '@/features/relay/contract/payload.gen'
import { Icon, type IconName } from '@/shared/ui/Icon'
import { SectionLabel } from '@/shared/ui/SectionLabel'

const SEARCH_SETTINGS_PATH = '/app/settings/search'
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
  const summary = summarizeTargets(targets)
  return (
    <section className={styles.block}>
      <SectionLabel
        aside={
          <Link to={SEARCH_SETTINGS_PATH} aria-label="Edit config" className={styles.edit}>
            <Icon name="pencil" />
            Edit
          </Link>
        }
      >
        Config
      </SectionLabel>
      <div className={styles.facts}>
        <Fact icon="level" label="Level" value={summary.level} />
        <Fact icon="clock" label="Experience asked" value={summary.years} />
        <Fact icon="cap" label="Master's" value={summary.masters} />
        <Fact icon="place" label="Places" value={summary.places} />
      </div>
    </section>
  )
}
