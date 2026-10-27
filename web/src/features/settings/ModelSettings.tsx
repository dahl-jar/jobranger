import { useId, useRef, useState, type ReactNode } from 'react'
import { flushSync } from 'react-dom'
import type {
  ConfigPatch,
  ConfigSnapshot,
  ModelInfo,
  ModelsSnapshot,
  ProviderId,
  ProviderModels,
  UsageSnapshot,
} from '@/features/relay/contract/payload.gen'
import { CheckRow } from '@/features/settings/CheckRow'
import { MonthlyLimit } from '@/features/settings/MonthlyLimit'
import { PriceFields } from '@/features/settings/PriceFields'
import { ReplaceKeyForm } from '@/features/settings/ReplaceKeyForm'
import { SettingsFieldset } from '@/features/settings/SettingsFieldset'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import styles from '@/features/settings/Settings.module.css'
import { formatUsd } from '@/shared/format/money'
import { Button } from '@/shared/ui/Button'

const PROVIDERS: readonly { id: ProviderId; label: string }[] = [
  { id: 'anthropic', label: 'Anthropic' },
  { id: 'openai', label: 'OpenAI' },
  { id: 'openrouter', label: 'OpenRouter' },
  { id: 'ollama', label: 'Ollama' },
]

const KEYLESS_PROVIDER: ProviderId = 'ollama'

function modelLabel(model: ModelInfo): string {
  const cost = model.usd_per_day === null ? 'price unknown' : `about ${formatUsd(model.usd_per_day)} a normal day`
  return `${model.name} · ${cost}`
}

function missingHint(provider: ProviderId): string {
  return provider === KEYLESS_PROVIDER ? 'Not found on this machine' : 'No key yet'
}

function providerRadioId(idPrefix: string, provider: ProviderId): string {
  return `${idPrefix}-${provider}`
}

interface ModelSelectProps {
  entry: ProviderModels
  selected: string
  onSelect: (provider: ProviderId, model: string) => void
}

function ModelSelect({ entry, selected, onSelect }: ModelSelectProps) {
  const selectId = useId()
  const isListed = entry.models.some((model) => model.id === selected)

  return (
    <section className={styles.group}>
      <div className={styles.field}>
        <label htmlFor={selectId} className={styles.label}>
          Model
        </label>
        <div className={styles.rule} />
        <select
          id={selectId}
          className={`${styles.select} ${styles.stack}`}
          value={selected}
          onChange={(event) => onSelect(entry.provider, event.target.value)}
        >
          {selected === '' && (
            <option value="" disabled>
              Pick a model
            </option>
          )}
          {selected !== '' && !isListed && <option value={selected}>{selected} (not listed)</option>}
          {entry.models.map((model) => (
            <option key={model.id} value={model.id}>
              {modelLabel(model)}
            </option>
          ))}
        </select>
      </div>
    </section>
  )
}

interface ModelSettingsProps {
  config: ConfigSnapshot
  models: ModelsSnapshot
  usage: UsageSnapshot
  usageTable: ReactNode
  onSelectModel: (provider: ProviderId, model: string) => void
  onReplaceKey: (provider: ProviderId, key: string) => void
  onPatch: (patch: ConfigPatch) => void
}

export function ModelSettings({ config, models, usage, usageTable, onSelectModel, onReplaceKey, onPatch }: ModelSettingsProps) {
  const idPrefix = useId()
  const [picked, setPicked] = useState(config.model.provider)
  const [isReplacing, setReplacing] = useState(false)
  const replaceRef = useRef<HTMLButtonElement>(null)
  const entry = models.providers.find((provider) => provider.provider === picked)
  const isConfigured = picked === config.model.provider
  const selectedModel = isConfigured ? (config.model.model ?? '') : ''

  function pick(provider: ProviderId | null) {
    setPicked(provider)
    setReplacing(false)
  }

  function closeReplace() {
    flushSync(() => setReplacing(false))
    replaceRef.current?.focus()
  }

  function cancelKey() {
    if (entry !== undefined) return closeReplace()
    const back = config.model.provider ?? picked
    flushSync(() => pick(config.model.provider))
    if (back !== null) document.getElementById(providerRadioId(idPrefix, back))?.focus()
  }

  function saveKey(provider: ProviderId, key: string) {
    onReplaceKey(provider, key)
    closeReplace()
  }

  return (
    <>
      <SettingsFieldset legend="Provider">
        <div className={styles.stack}>
          {PROVIDERS.map((provider) => {
            const hasEntry = models.providers.some((candidate) => candidate.provider === provider.id)
            return (
              <CheckRow
                key={provider.id}
                id={providerRadioId(idPrefix, provider.id)}
                type="radio"
                name="provider"
                label={provider.label}
                hint={hasEntry ? undefined : missingHint(provider.id)}
                checked={picked === provider.id}
                onChange={() => pick(provider.id)}
              />
            )
          })}
        </div>
      </SettingsFieldset>
      {entry !== undefined && entry.models.length > 0 && <ModelSelect entry={entry} selected={selectedModel} onSelect={onSelectModel} />}
      {picked !== null && picked !== KEYLESS_PROVIDER && (
        <SettingsGroup title="API key">
          {entry !== undefined && entry.error !== null && (
            <p role="alert" className={styles.error}>
              {entry.error}
            </p>
          )}
          {entry === undefined || isReplacing ? (
            <ReplaceKeyForm focusOnOpen={isReplacing} onSave={(key) => saveKey(picked, key)} onCancel={cancelKey} />
          ) : (
            <div className={styles.toolRow}>
              {isConfigured && config.model.key_hint !== null && <span className={styles.strong}>Key ending {config.model.key_hint}</span>}
              <Button ref={replaceRef} variant="quiet" size={36} onClick={() => setReplacing(true)}>
                Replace key
              </Button>
            </div>
          )}
          <p className={styles.note}>Stored on your machine only.</p>
        </SettingsGroup>
      )}
      <SettingsGroup title="Usage">{usageTable}</SettingsGroup>
      <MonthlyLimit
        limitUsd={config.model.monthly_limit_usd}
        monthUsd={usage.month_usd}
        onChange={(limit) => onPatch({ model: { monthly_limit_usd: limit } })}
      />
      <PriceFields price={config.model.price} onChange={(price) => onPatch({ model: { price } })} />
    </>
  )
}
