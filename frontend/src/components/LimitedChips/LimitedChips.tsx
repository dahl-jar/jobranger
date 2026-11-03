import { useId, useState } from 'react'
import type { ChipLimit } from '@/constants/chipInputLimits'
import styles from '@/components/SettingsFormStyles/settingsForm.module.css'
import { useText } from '@/hooks/useLanguage'
import { ChipInput } from '@/components/ChipInput/ChipInput'

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
