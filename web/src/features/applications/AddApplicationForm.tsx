import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import styles from '@/features/applications/Applications.module.css'
import { COMPANY_MAX_CHARS, NOTE_MAX_CHARS, TITLE_MAX_CHARS } from '@/features/applications/limits'
import type { ApplicationCreate } from '@/features/relay/contract/payload.gen'
import { Button } from '@/shared/ui/Button'
import button from '@/shared/ui/button.module.css'

export type ApplicationDraft = Pick<ApplicationCreate, 'company' | 'title' | 'note'>

interface AddApplicationFormProps {
  onAdd: (draft: ApplicationDraft) => Promise<void>
  onCancel: () => void
  disabled: boolean
}

type FormError = { kind: 'company' } | { kind: 'save'; message: string }

const EMPTY_DRAFT: ApplicationDraft = { company: '', title: '', note: '' }

const FIELDS = [
  { name: 'company', label: 'Company', max: COMPANY_MAX_CHARS, className: '', placeholder: undefined },
  { name: 'title', label: 'Role', max: TITLE_MAX_CHARS, className: styles.roleField, placeholder: undefined },
  { name: 'note', label: 'Note', max: NOTE_MAX_CHARS, className: styles.noteField, placeholder: 'e.g. Applied through a referral' },
] as const

function describedBy(ids: (string | false)[]): string | undefined {
  const present = ids.filter((id) => id !== false)
  return present.length > 0 ? present.join(' ') : undefined
}

export function AddApplicationForm({ onAdd, onCancel, disabled }: AddApplicationFormProps) {
  const idPrefix = useId()
  const companyRef = useRef<HTMLInputElement>(null)
  const [draft, setDraft] = useState<ApplicationDraft>(EMPTY_DRAFT)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<FormError | undefined>(undefined)
  const companyErrorId = `${idPrefix}-company-error`
  const companyMissing = error?.kind === 'company'

  useEffect(() => {
    companyRef.current?.focus()
  }, [])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmed = { company: draft.company.trim(), title: draft.title.trim(), note: draft.note.trim() }
    if (!trimmed.company) {
      setError({ kind: 'company' })
      companyRef.current?.focus()
      return
    }
    setError(undefined)
    setSaving(true)
    try {
      await onAdd(trimmed)
      setDraft(EMPTY_DRAFT)
      companyRef.current?.focus()
    } catch (caught: unknown) {
      setError({ kind: 'save', message: caught instanceof Error ? caught.message : String(caught) })
    }
    setSaving(false)
  }

  return (
    <form aria-label="Add an application" className={styles.addForm} noValidate onSubmit={(event) => void submit(event)}>
      {FIELDS.map((field) => {
        const value = draft[field.name]
        const limitId = `${idPrefix}-${field.name}-limit`
        const atLimit = value.length >= field.max
        const isCompany = field.name === 'company'
        const inputId = `${idPrefix}-${field.name}`
        return (
          <div key={field.name} className={`${styles.field} ${field.className}`}>
            <label htmlFor={inputId} className={styles.fieldLabel}>
              {field.label}
            </label>
            <input
              id={inputId}
              ref={isCompany ? companyRef : undefined}
              type="text"
              value={value}
              maxLength={field.max}
              placeholder={field.placeholder}
              aria-invalid={isCompany ? companyMissing : undefined}
              aria-describedby={describedBy([isCompany && companyMissing && companyErrorId, atLimit && limitId])}
              className={styles.input}
              onChange={(event) => setDraft({ ...draft, [field.name]: event.target.value.slice(0, field.max) })}
            />
            {atLimit && (
              <span id={limitId} className={styles.limit}>
                {field.label} stops at {field.max} characters.
              </span>
            )}
          </div>
        )
      })}
      <Button type="submit" size={40} className={`${button.compact} ${button.medium}`} disabled={disabled || saving}>
        {saving ? 'Adding...' : 'Add'}
      </Button>
      <button type="button" className={styles.cancelButton} onClick={onCancel}>
        Cancel
      </button>
      {companyMissing && (
        <p id={companyErrorId} role="alert" className={styles.formError}>
          Enter a company.
        </p>
      )}
      {error?.kind === 'save' && (
        <p role="alert" className={styles.formError}>
          Not saved: {error.message}
        </p>
      )}
    </form>
  )
}
