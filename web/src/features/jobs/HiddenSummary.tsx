import { Link } from 'react-router'
import styles from '@/features/jobs/JobsView.module.css'
import { listPath } from '@/features/jobs/paths'
import type { HiddenBreakdown } from '@/features/jobs/selectors'

const PART_SEPARATOR = ' · '

export function HiddenSummary({ breakdown }: { breakdown: HiddenBreakdown }) {
  if (breakdown.total === 0) return null
  const parts = breakdown.parts.map((part) => `${part.count} ${part.label}`).join(PART_SEPARATOR)
  return (
    <p className={styles.hiddenSummary}>
      {parts === '' ? `${breakdown.total} hidden` : `${breakdown.total} hidden: ${parts}`}
      {PART_SEPARATOR}
      <Link to={listPath('hidden')}>view</Link>
    </p>
  )
}
