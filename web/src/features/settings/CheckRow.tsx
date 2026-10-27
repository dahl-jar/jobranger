import { useId, type Ref } from 'react'
import styles from '@/features/settings/Settings.module.css'

interface CheckRowProps {
  label: string
  hint?: string | undefined
  checked: boolean
  disabled?: boolean
  type?: 'checkbox' | 'radio'
  name?: string
  id?: string
  lang?: string
  onChange: (checked: boolean) => void
  ref?: Ref<HTMLInputElement>
}

export function CheckRow({ label, hint, checked, disabled = false, type = 'checkbox', name, id, lang, onChange, ref }: CheckRowProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const hintId = `${inputId}-hint`

  return (
    <div className={styles.check}>
      <input
        ref={ref}
        id={inputId}
        type={type}
        name={name}
        checked={checked}
        disabled={disabled}
        aria-describedby={hint === undefined ? undefined : hintId}
        onChange={(event) => onChange(event.target.checked)}
      />
      <label htmlFor={inputId} lang={lang} className={styles.optionLabel}>
        {label}
      </label>
      {hint !== undefined && (
        <span id={hintId} className={styles.hint}>
          {hint}
        </span>
      )}
    </div>
  )
}
