import { useId, useRef, useState } from 'react'
import type { ConfigPatch, ConfigSnapshot, Sources, SourcesPatch } from '@/features/relay/contract/payload.gen'
import { payloadSchema } from '@/features/relay/contract/schemas.gen'
import { CheckRow } from '@/features/settings/CheckRow'
import { InlineConfirm } from '@/features/settings/InlineConfirm'
import { useDraft } from '@/features/settings/useDraft'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { MIN_GAP_HOURS, nextFreeTime, sortTimes, validateRunTimes, type RunTimesError } from '@/features/settings/runTimes'
import styles from '@/features/settings/Settings.module.css'
import { Icon } from '@/shared/ui/Icon'

const EMAIL_PATTERN = new RegExp(payloadSchema.$defs.Email.pattern)
const EMAIL_MAX_CHARS = payloadSchema.$defs.Email.maxLength
const PERSONAL_USE_HINT = 'Off by default. For personal use only.'

const SOURCE_OPTIONS: readonly { key: keyof Sources; label: string; hint?: string }[] = [
  { key: 'nav', label: 'NAV' },
  { key: 'employers', label: 'Employer feeds', hint: 'Teamtailor and Ashby' },
  { key: 'finn', label: 'FINN', hint: PERSONAL_USE_HINT },
  { key: 'linkedin', label: 'LinkedIn', hint: PERSONAL_USE_HINT },
]

type ChannelNotice = 'keepOne' | 'needAddress' | 'badAddress' | 'addressInUse'

const CHANNEL_NOTICES: Record<ChannelNotice, string> = {
  keepOne: 'Keep at least one way to get matches.',
  needAddress: 'Enter an address first.',
  badAddress: 'Check the address.',
  addressInUse: 'Turn the email off before clearing the address.',
}

interface SectionProps {
  config: ConfigSnapshot
  onPatch: (patch: ConfigPatch) => void
}

function isValidAddress(address: string): boolean {
  return address.length <= EMAIL_MAX_CHARS && EMAIL_PATTERN.test(address)
}

function MatchChannels({ config, onPatch }: SectionProps) {
  const addressId = useId()
  const { website, email } = config.notify
  const savedAddress = email.address ?? ''
  const [address, setAddress] = useDraft(savedAddress)
  const [isConfirmingOff, setConfirmingOff] = useState(false)
  const [notice, setNotice] = useState<ChannelNotice>()
  const websiteRef = useRef<HTMLInputElement>(null)

  function send(patch: ConfigPatch) {
    setNotice(undefined)
    onPatch(patch)
  }

  function toggleWebsite(checked: boolean) {
    if (checked) return send({ notify: { website: true } })
    if (!email.on) return setNotice('keepOne')
    setConfirmingOff(true)
  }

  function closeWebsiteQuestion() {
    setConfirmingOff(false)
    websiteRef.current?.focus()
  }

  function turnWebsiteOff() {
    closeWebsiteQuestion()
    send({ notify: { website: false } })
  }

  function toggleEmail(checked: boolean) {
    if (!checked) {
      if (!website) return setNotice('keepOne')
      return send({ notify: { email: { on: false } } })
    }
    if (address === '') return setNotice('needAddress')
    if (!isValidAddress(address)) return setNotice('badAddress')
    if (address === savedAddress) return send({ notify: { email: { on: true } } })
    send({ notify: { email: { on: true, address } } })
  }

  function commitAddress() {
    if (address === savedAddress) return
    if (address === '') {
      if (email.on) return setNotice('addressInUse')
      return send({ notify: { email: { address: null } } })
    }
    if (!isValidAddress(address)) return setNotice('badAddress')
    send({ notify: { email: { address } } })
  }

  return (
    <>
      <SettingsGroup title="Website">
        <div className={styles.stack}>
          <CheckRow ref={websiteRef} label="Show matches on this website" checked={website} onChange={toggleWebsite} />
          {isConfirmingOff && (
            <InlineConfirm
              question="This page stops getting updates. Turn off?"
              confirmLabel="Turn off"
              onConfirm={turnWebsiteOff}
              onCancel={closeWebsiteQuestion}
            />
          )}
        </div>
      </SettingsGroup>
      <SettingsGroup title="Daily email">
        <div className={styles.stack}>
          <CheckRow label="Send me the daily list by email" checked={email.on} onChange={toggleEmail} />
          <div className={styles.field}>
            <label htmlFor={addressId}>Address</label>
            <input
              id={addressId}
              type="email"
              autoComplete="email"
              className={styles.textInput}
              value={address}
              aria-invalid={notice === 'badAddress'}
              onChange={(event) => setAddress(event.target.value)}
              onBlur={commitAddress}
            />
          </div>
        </div>
        {notice !== undefined && (
          <p role="alert" className={styles.error}>
            {CHANNEL_NOTICES[notice]}
          </p>
        )}
      </SettingsGroup>
    </>
  )
}

function SourceToggles({ config, onPatch }: SectionProps) {
  const [isLastSource, setLastSource] = useState(false)
  const { sources } = config

  function toggle(key: keyof Sources, checked: boolean) {
    const onCount = SOURCE_OPTIONS.filter((option) => sources[option.key]).length
    if (!checked && onCount === 1) return setLastSource(true)
    setLastSource(false)
    const patch: SourcesPatch = { [key]: checked }
    onPatch({ sources: patch })
  }

  return (
    <SettingsGroup title="Sources">
      <div className={styles.stack}>
        {SOURCE_OPTIONS.map((option) => (
          <CheckRow
            key={option.key}
            label={option.label}
            hint={option.hint}
            checked={sources[option.key]}
            onChange={(checked) => toggle(option.key, checked)}
          />
        ))}
      </div>
      {isLastSource && (
        <p role="alert" className={styles.error}>
          Keep at least one source on.
        </p>
      )}
    </SettingsGroup>
  )
}

function sameTimes(first: readonly string[], second: readonly string[]): boolean {
  return first.join() === second.join()
}

function runTimesMessage(error: RunTimesError): string {
  switch (error.kind) {
    case 'format':
      return 'Use a time like 03:00.'
    case 'duplicate':
      return `${error.time} is in the list twice.`
    case 'tooClose':
      return `${error.pair[0]} and ${error.pair[1]} are less than ${MIN_GAP_HOURS} hours apart.`
  }
}

function RunTimes({ times, onPatch }: { times: readonly string[]; onPatch: (patch: ConfigPatch) => void }) {
  const [drafts, setDrafts] = useDraft<readonly string[]>(times, sameTimes)
  const error = validateRunTimes(drafts)
  const nextTime = error === undefined ? nextFreeTime(drafts) : undefined

  function send(next: readonly string[]) {
    onPatch({ schedule: { times: sortTimes(next) } })
  }

  function commit() {
    if (error !== undefined || sameTimes(drafts, times)) return
    send(drafts)
  }

  function remove(index: number) {
    const remaining = drafts.filter((_, position) => position !== index)
    if (validateRunTimes(remaining) !== undefined) return setDrafts(remaining)
    send(remaining)
  }

  return (
    <SettingsGroup title="Run times">
      <ul className={styles.plainList}>
        {drafts.map((time, index) => (
          <li key={index} className={styles.row}>
            <input
              type="time"
              aria-label={`Run time ${index + 1}`}
              className={styles.textInput}
              value={time}
              onChange={(event) => setDrafts(drafts.map((draft, position) => (position === index ? event.target.value : draft)))}
              onBlur={commit}
            />
            <span className={styles.grow}>every day</span>
            <button type="button" className={styles.textButton} aria-label={`Remove run time ${index + 1}`} onClick={() => remove(index)}>
              <Icon name="close" />
              Remove
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        className={styles.addButton}
        disabled={nextTime === undefined}
        onClick={() => {
          if (nextTime !== undefined) send([...drafts, nextTime])
        }}
      >
        <Icon name="plus" />
        Add a run time
      </button>
      {error !== undefined && (
        <p role="alert" className={styles.error}>
          {runTimesMessage(error)}
        </p>
      )}
      <p className={styles.note}>{`At least ${MIN_GAP_HOURS} hours apart.`}</p>
    </SettingsGroup>
  )
}

export function DeliverySettings({ config, onPatch }: SectionProps) {
  return (
    <>
      <MatchChannels config={config} onPatch={onPatch} />
      <SourceToggles config={config} onPatch={onPatch} />
      <RunTimes times={config.schedule.times} onPatch={onPatch} />
    </>
  )
}
