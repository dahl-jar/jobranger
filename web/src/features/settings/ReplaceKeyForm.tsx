import { useId, useState, type FormEvent } from 'react'
import { payloadSchema } from '@/features/relay/contract/schemas.gen'
import { focusOnMount } from '@/features/settings/focusOnMount'
import styles from '@/features/settings/Settings.module.css'
import { Button } from '@/shared/ui/Button'

const KEY_PATTERN = new RegExp(payloadSchema.$defs.KeyReplace.properties.key.pattern)
const KEY_MIN_CHARS = 12
const KEY_FIELD = 'key'

interface ReplaceKeyFormProps {
  focusOnOpen: boolean
  onSave: (key: string) => void
  onCancel: () => void
}

export function ReplaceKeyForm({ focusOnOpen, onSave, onCancel }: ReplaceKeyFormProps) {
  const inputId = useId()
  const [isInvalid, setInvalid] = useState(false)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const typed = new FormData(form).get(KEY_FIELD)
    const key = typeof typed === 'string' ? typed.trim() : ''
    if (!KEY_PATTERN.test(key)) {
      setInvalid(true)
      return
    }
    setInvalid(false)
    form.reset()
    onSave(key)
  }

  return (
    <form className={styles.stack} onSubmit={submit}>
      <div className={styles.field}>
        <label htmlFor={inputId}>New API key</label>
        <input
          ref={focusOnOpen ? focusOnMount : undefined}
          id={inputId}
          name={KEY_FIELD}
          type="password"
          autoComplete="new-password"
          spellCheck={false}
          className={styles.textInput}
          aria-invalid={isInvalid}
        />
      </div>
      {isInvalid && (
        <p role="alert" className={styles.error}>
          {`An API key has ${KEY_MIN_CHARS} or more characters and no spaces.`}
        </p>
      )}
      <div className={styles.toolRow}>
        <Button type="submit" size={40}>
          Save key
        </Button>
        <button type="button" className={styles.textButton} onClick={() => onCancel()}>
          Cancel
        </button>
      </div>
    </form>
  )
}
