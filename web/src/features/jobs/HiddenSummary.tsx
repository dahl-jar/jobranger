import { Link } from 'react-router'
import styles from '@/features/jobs/JobsView.module.css'
import { listPath } from '@/features/jobs/paths'
import type { HiddenBreakdown, HiddenPart } from '@/features/jobs/selectors'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'

const PART_SEPARATOR = ' · '

function countedPart(part: HiddenPart, words: Messages['jobs']['hidden']): string {
  return part.kind === 'years' ? words.part(part.count, part.label) : words.counted[part.kind](part.count)
}

export function HiddenSummary({ breakdown }: { breakdown: HiddenBreakdown }) {
  const words = useText().jobs.hidden
  if (breakdown.total === 0) return null
  const parts = breakdown.parts.map((part) => countedPart(part, words)).join(PART_SEPARATOR)
  return (
    <p className={styles.hiddenSummary}>
      {parts === '' ? words.count(breakdown.total) : words.withParts(breakdown.total, parts)}
      {PART_SEPARATOR}
      <Link to={listPath('hidden')}>{words.view}</Link>
    </p>
  )
}
