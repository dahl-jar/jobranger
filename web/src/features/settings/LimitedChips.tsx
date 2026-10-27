import { useId, useState } from 'react'
import type { ChipLimit } from '@/features/settings/chipLimits'
import styles from '@/features/settings/Settings.module.css'
import { useText } from '@/shared/i18n/languageContext'
import { ChipInput } from '@/shared/ui/ChipInput'

interface LimitedChipsProps {
  label: string
  items: readonly string[]
  limit: ChipLimit
  placeholder: string
  className?: string | undefined
  onChange: (items: string[]) => void
}

export function LimitedChips({ label, items, limit, placeholder, className, onChange }: LimitedChipsProps) {
  const inputId = useId()
  const chips = useText().settings.chips
  const nouns = chips[limit.kind]
  const [isTooLong, setTooLong] = useState(false)

  function change(next: string[]) {
    const tooLong = next.some((item) => item.length > limit.maxLength)
    setTooLong(tooLong)
    if (!tooLong) onChange(next)
  }

  return (
    <>
      <ChipInput
        id={inputId}
        label={label}
        items={items}
        placeholder={placeholder}
        maxLength={limit.maxLength}
        maxItems={limit.maxItems}
        className={className ?? ''}
        onChange={change}
      />
      {items.length >= limit.maxItems && <span className={styles.hint}>{nouns.atMost(limit.maxItems)}</span>}
      {isTooLong && (
        <p role="alert" className={styles.error}>
          {chips.tooLong(nouns.item, limit.maxLength)}
        </p>
      )}
    </>
  )
}
