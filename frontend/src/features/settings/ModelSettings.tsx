import { useId, useRef, useState, type ReactNode, type Ref } from 'react'
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
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Button } from '@/shared/ui/Button'

const PROVIDERS: readonly { id: ProviderId; label: string }[] = [
  { id: 'anthropic', label: 'Anthropic' },
  { id: 'openai', label: 'OpenAI' },
  { id: 'openrouter', label: 'OpenRouter' },
  { id: 'ollama', label: 'Ollama' },
]

const KEYLESS_PROVIDER: ProviderId = 'ollama'

function modelLabel(model: ModelInfo, text: Messages): string {
  const words = text.settings.model
  const cost = model.usd_per_day === null ? words.priceUnknown : words.perDay(formatUsd(model.usd_per_day, text))
  return `${model.name} · ${cost}`
}

function missingHint(provider: ProviderId, text: Messages): string {
  return provider === KEYLESS_PROVIDER ? text.settings.model.notFound : text.settings.model.noKey
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
  const text = useText()
  const words = text.settings.model
  const isListed = entry.models.some((model) => model.id === selected)

  return (
    <section className={styles.group}>
      <div className={styles.field}>
        <label htmlFor={selectId} className={styles.label}>
          {words.model}
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
              {words.pick}
            </option>
          )}
          {selected !== '' && !isListed && <option value={selected}>{words.notListed(selected)}</option>}
          {entry.models.map((model) => (
            <option key={model.id} value={model.id}>
              {modelLabel(model, text)}
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

interface ProviderPickerProps {
  idPrefix: string
  models: ModelsSnapshot
  picked: ProviderId | null
  onPick: (provider: ProviderId) => void
}

function ProviderPicker({ idPrefix, models, picked, onPick }: ProviderPickerProps) {
  const text = useText()
  return (
    <SettingsFieldset legend={text.settings.model.provider}>
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
              hint={hasEntry ? undefined : missingHint(provider.id, text)}
              checked={picked === provider.id}
              onChange={() => onPick(provider.id)}
            />
          )
        })}
      </div>
    </SettingsFieldset>
  )
}

interface KeySectionProps {
  entry: ProviderModels | undefined
  keyHint: string | null
  isReplacing: boolean
  replaceRef: Ref<HTMLButtonElement>
  onReplace: () => void
  onSave: (key: string) => void
  onCancel: () => void
}

function KeySection({ entry, keyHint, isReplacing, replaceRef, onReplace, onSave, onCancel }: KeySectionProps) {
  const words = useText().settings.model
  return (
    <SettingsGroup title={words.apiKey}>
      {entry !== undefined && entry.error !== null && (
        <p role="alert" className={styles.error}>
          {entry.error}
        </p>
      )}
      {entry === undefined || isReplacing ? (
        <ReplaceKeyForm focusOnOpen={isReplacing} onSave={onSave} onCancel={onCancel} />
      ) : (
        <div className={styles.toolRow}>
          {keyHint !== null && <span className={styles.strong}>{words.keyEnding(keyHint)}</span>}
          <Button ref={replaceRef} variant="quiet" size={36} onClick={onReplace}>
            {words.replaceKey}
          </Button>
        </div>
      )}
      <p className={styles.note}>{words.storedLocally}</p>
    </SettingsGroup>
  )
}

export function ModelSettings({ config, models, usage, usageTable, onSelectModel, onReplaceKey, onPatch }: ModelSettingsProps) {
  const idPrefix = useId()
  const words = useText().settings.model
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
      <ProviderPicker idPrefix={idPrefix} models={models} picked={picked} onPick={pick} />
      {entry !== undefined && entry.models.length > 0 && <ModelSelect entry={entry} selected={selectedModel} onSelect={onSelectModel} />}
      {picked !== null && picked !== KEYLESS_PROVIDER && (
        <KeySection
          entry={entry}
          keyHint={isConfigured ? config.model.key_hint : null}
          isReplacing={isReplacing}
          replaceRef={replaceRef}
          onReplace={() => setReplacing(true)}
          onSave={(key) => saveKey(picked, key)}
          onCancel={cancelKey}
        />
      )}
      <SettingsGroup title={words.usage}>{usageTable}</SettingsGroup>
      <MonthlyLimit
        limitUsd={config.model.monthly_limit_usd}
        monthUsd={usage.month_usd}
        onChange={(limit) => onPatch({ model: { monthly_limit_usd: limit } })}
      />
      <PriceFields price={config.model.price} onChange={(price) => onPatch({ model: { price } })} />
    </>
  )
}
