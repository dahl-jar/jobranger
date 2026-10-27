import { useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react'
import { flushSync } from 'react-dom'
import { Icon } from '@/shared/ui/Icon'
import styles from '@/shared/ui/ChipInput.module.css'

const ADD_KEYS: readonly string[] = ['Enter', ',']
const REMOVE_ICON_SIZE = 12
const IME_KEY_CODE = 229
const PASTE_SEPARATOR = ','

function isListed(list: readonly string[], item: string): boolean {
  return list.some((existing) => existing.toLowerCase() === item.toLowerCase())
}

function appendItem(list: string[], candidate: string): string[] {
  const item = candidate.trim()
  return item === '' || isListed(list, item) ? list : [...list, item]
}

function takeItems(items: readonly string[], candidates: readonly string[], limit: number): { list: string[]; used: number } {
  return candidates.reduce(
    (taken, candidate) => (taken.list.length >= limit ? taken : { list: appendItem(taken.list, candidate), used: taken.used + 1 }),
    { list: [...items], used: 0 },
  )
}

interface ChipInputProps {
  id: string
  label: string
  items: readonly string[]
  onChange: (items: string[]) => void
  placeholder?: string
  maxLength?: number
  maxItems?: number
  className?: string
}

export function ChipInput({ id, label, items, onChange, placeholder, maxLength, maxItems = Number.POSITIVE_INFINITY, className }: ChipInputProps) {
  const [draft, setDraft] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)
  const removeButtons = useRef(new Map<string, HTMLButtonElement | null>())

  function addItems(candidates: readonly string[]): number {
    const { list, used } = takeItems(items, candidates, maxItems)
    if (list.length > items.length) {
      onChange(list)
    }
    return used
  }

  function addDraft() {
    if (addItems([draft]) > 0) {
      setDraft('')
    }
  }

  function removeAt(index: number, byPointer: boolean) {
    const neighbor = items[index + 1] ?? items[index - 1]
    if (neighbor !== undefined) {
      removeButtons.current.get(neighbor)?.focus()
    } else if (!byPointer) {
      inputRef.current?.focus()
    }
    onChange(items.filter((_, position) => position !== index))
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.nativeEvent.isComposing || event.keyCode === IME_KEY_CODE) {
      return
    }
    if (ADD_KEYS.includes(event.key)) {
      event.preventDefault()
      addDraft()
    } else if (event.key === 'Backspace' && draft === '' && items.length > 0) {
      onChange(items.slice(0, -1))
    }
  }

  function onPaste(event: ClipboardEvent<HTMLInputElement>) {
    const text = event.clipboardData.getData('text')
    if (!text.includes(PASTE_SEPARATOR)) {
      return
    }
    event.preventDefault()
    const input = event.currentTarget
    const start = input.selectionStart ?? draft.length
    const end = input.selectionEnd ?? start
    const after = draft.slice(end)
    const parts = `${draft.slice(0, start)}${text}${after}`.split(PASTE_SEPARATOR)
    const used = addItems(parts.slice(0, -1))
    const next = parts.slice(used).join(PASTE_SEPARATOR).trimStart()
    const caret = Math.max(next.length - after.length, 0)
    flushSync(() => setDraft(next))
    input.setSelectionRange(caret, caret)
  }

  return (
    <div className={className === undefined ? styles.field : `${styles.field} ${className}`}>
      <label htmlFor={id}>{label}</label>
      <div className={styles.box}>
        {items.map((item, index) => (
          <span key={item} className={styles.chip}>
            <span className={styles.label}>{item}</span>
            <button
              ref={(node) => {
                removeButtons.current.set(item, node)
              }}
              type="button"
              className={styles.remove}
              aria-label={`Remove ${item}`}
              onClick={(event) => removeAt(index, event.detail > 0)}
            >
              <Icon name="close" size={REMOVE_ICON_SIZE} />
            </button>
          </span>
        ))}
        <input
          ref={inputRef}
          id={id}
          type="text"
          className={styles.input}
          value={draft}
          placeholder={placeholder}
          maxLength={maxLength}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={onKeyDown}
          onBlur={addDraft}
          onPaste={onPaste}
        />
      </div>
    </div>
  )
}
