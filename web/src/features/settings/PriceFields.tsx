import { useId, useState } from 'react'
import type { Price } from '@/features/relay/contract/payload.gen'
import { payloadSchema } from '@/features/relay/contract/schemas.gen'
import { SettingsFieldset } from '@/features/settings/SettingsFieldset'
import styles from '@/features/settings/Settings.module.css'
import { useDraft } from '@/features/settings/useDraft'

const MAX_PRICE = payloadSchema.$defs.Money.maximum

function parsePrice(text: string): number | undefined {
  if (text === '') return undefined
  const value = Number(text)
  return value >= 0 && value <= MAX_PRICE ? value : undefined
}

export function PriceFields({ price, onChange }: { price: Price; onChange: (price: Price) => void }) {
  const idPrefix = useId()
  const [input, setInput] = useDraft(String(price.input_per_mtok))
  const [output, setOutput] = useDraft(String(price.output_per_mtok))
  const [isInvalid, setInvalid] = useState(false)

  function commit() {
    const inputPrice = parsePrice(input)
    const outputPrice = parsePrice(output)
    if (inputPrice === undefined || outputPrice === undefined) {
      setInvalid(true)
      return
    }
    setInvalid(false)
    if (inputPrice === price.input_per_mtok && outputPrice === price.output_per_mtok) return
    onChange({ input_per_mtok: inputPrice, output_per_mtok: outputPrice })
  }

  const fields = [
    { label: 'Input', value: input, setValue: setInput },
    { label: 'Output', value: output, setValue: setOutput },
  ]

  return (
    <SettingsFieldset legend="Price">
      <div className={styles.stack}>
        <span>USD per 1M tokens</span>
        <div className={styles.actions}>
          {fields.map((field) => (
            <div key={field.label} className={styles.field}>
              <label htmlFor={`${idPrefix}-${field.label}`}>{field.label}</label>
              <span className={styles.money}>
                $
                <input
                  id={`${idPrefix}-${field.label}`}
                  type="number"
                  min="0"
                  step="0.01"
                  value={field.value}
                  aria-invalid={isInvalid}
                  onChange={(event) => field.setValue(event.target.value)}
                  onBlur={commit}
                />
              </span>
            </div>
          ))}
        </div>
      </div>
      {isInvalid && (
        <p role="alert" className={styles.error}>
          Enter a price from 0 to {MAX_PRICE}.
        </p>
      )}
      <p className={styles.note}>Estimates. Your provider&apos;s bill is the real cost.</p>
    </SettingsFieldset>
  )
}
