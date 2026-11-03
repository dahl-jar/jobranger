import { Link } from 'react-router'
import styles from '@/components/JobsView/JobsView.module.css'
import { useAppBase } from '@/hooks/useAppBase'
import { listPath } from '@/helpers/adPaths'
import type { HiddenBreakdown, HiddenPart } from '@/helpers/adSelectors'
import type { Messages } from '@/i18n/en'
import { useText } from '@/hooks/useLanguage'

const PART_SEPARATOR = ' · '

function countedPart(part: HiddenPart, words: Messages['jobs']['hidden']): string {
  return part.kind === 'years' ? words.part(part.count, part.label) : words.counted[part.kind](part.count)
}

export function HiddenSummary({ breakdown }: { breakdown: HiddenBreakdown }) {
  const words = useText().jobs.hidden
  const base = useAppBase()
  if (breakdown.total === 0) return null
  const parts = breakdown.parts.map((part) => countedPart(part, words)).join(PART_SEPARATOR)
  return (
    <p className={styles.hiddenSummary}>
      {parts === '' ? words.count(breakdown.total) : words.withParts(breakdown.total, parts)}
      {PART_SEPARATOR}
      <Link to={listPath('hidden', base)}>{words.view}</Link>
    </p>
  )
}
