import { useId, useRef, useState } from 'react'
import type { ConfigPatch, ConfigSnapshot, Sources, SourcesPatch } from '@/features/relay/contract/payload.gen'
import { payloadSchema } from '@/features/relay/contract/schemas.gen'
import { CheckRow } from '@/features/settings/CheckRow'
import { InlineConfirm } from '@/features/settings/InlineConfirm'
import { useDraft } from '@/features/settings/useDraft'
import { SettingsGroup } from '@/features/settings/SettingsGroup'
import { MIN_GAP_HOURS, nextFreeTime, sortTimes, validateRunTimes, type RunTimesError } from '@/features/settings/runTimes'
import styles from '@/features/settings/Settings.module.css'
import type { Messages } from '@/shared/i18n/en'
import { useText } from '@/shared/i18n/languageContext'
import { Icon } from '@/shared/ui/Icon'
import { SOURCE_NAMES } from '@/shared/labels/sources'

const EMAIL_PATTERN = new RegExp(payloadSchema.$defs.Email.pattern)
const EMAIL_MAX_CHARS = payloadSchema.$defs.Email.maxLength
const SOURCE_KEYS: readonly (keyof Sources)[] = ['nav', 'employers', 'finn', 'linkedin']

type ChannelNotice = 'keepOne' | 'needAddress' | 'badAddress' | 'addressInUse'

function sourceOption(key: keyof Sources, text: Messages): { label: string; hint: string | undefined } {
  const words = text.settings.delivery
  switch (key) {
    case 'nav':
      return { label: SOURCE_NAMES.nav, hint: undefined }
    case 'employers':
      return { label: words.employers, hint: words.employersHint }
    case 'finn':
      return { label: SOURCE_NAMES.finn, hint: words.personalUse }
    case 'linkedin':
      return { label: SOURCE_NAMES.linkedin, hint: words.personalUse }
  }
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
  const words = useText().settings.delivery

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
      <SettingsGroup title={words.website}>
        <div className={styles.stack}>
          <CheckRow ref={websiteRef} label={words.showOnWebsite} checked={website} onChange={toggleWebsite} />
          {isConfirmingOff && (
            <InlineConfirm
              question={words.confirmOff}
              confirmLabel={words.turnOff}
              onConfirm={turnWebsiteOff}
              onCancel={closeWebsiteQuestion}
            />
          )}
        </div>
      </SettingsGroup>
      <SettingsGroup title={words.email}>
        <div className={styles.stack}>
          <CheckRow label={words.sendEmail} checked={email.on} onChange={toggleEmail} />
          <div className={styles.field}>
            <label htmlFor={addressId}>{words.address}</label>
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
            {words.notices[notice]}
          </p>
        )}
      </SettingsGroup>
    </>
  )
}

function SourceToggles({ config, onPatch }: SectionProps) {
  const [isLastSource, setLastSource] = useState(false)
  const text = useText()
  const { sources } = config

  function toggle(key: keyof Sources, checked: boolean) {
    const enabledCount = SOURCE_KEYS.filter((option) => sources[option]).length
    if (!checked && enabledCount === 1) return setLastSource(true)
    setLastSource(false)
    const patch: SourcesPatch = { [key]: checked }
    onPatch({ sources: patch })
  }

  return (
    <SettingsGroup title={text.settings.delivery.sources}>
      <div className={styles.stack}>
        {SOURCE_KEYS.map((key) => {
          const option = sourceOption(key, text)
          return (
            <CheckRow key={key} label={option.label} hint={option.hint} checked={sources[key]} onChange={(checked) => toggle(key, checked)} />
          )
        })}
      </div>
      {isLastSource && (
        <p role="alert" className={styles.error}>
          {text.settings.delivery.lastSource}
        </p>
      )}
    </SettingsGroup>
  )
}

function sameTimes(first: readonly string[], second: readonly string[]): boolean {
  return first.join() === second.join()
}

function runTimesMessage(error: RunTimesError, text: Messages): string {
  const words = text.settings.delivery
  switch (error.kind) {
    case 'format':
      return words.timeFormat
    case 'duplicate':
      return words.duplicate(error.time)
    case 'tooClose':
      return words.tooClose(error.pair[0], error.pair[1], MIN_GAP_HOURS)
  }
}

function RunTimes({ times, onPatch }: { times: readonly string[]; onPatch: (patch: ConfigPatch) => void }) {
  const [drafts, setDrafts] = useDraft<readonly string[]>(times, sameTimes)
  const error = validateRunTimes(drafts)
  const nextTime = error === undefined ? nextFreeTime(drafts) : undefined
  const text = useText()
  const words = text.settings.delivery

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
    <SettingsGroup title={words.runTimes}>
      <ul className={styles.plainList}>
        {drafts.map((time, index) => (
          <li key={index} className={styles.row}>
            <input
              type="time"
              aria-label={words.runTime(index + 1)}
              className={styles.textInput}
              value={time}
              onChange={(event) => setDrafts(drafts.map((draft, position) => (position === index ? event.target.value : draft)))}
              onBlur={commit}
            />
            <span className={styles.grow}>{words.everyDay}</span>
            <button type="button" className={styles.textButton} aria-label={words.removeRunTime(index + 1)} onClick={() => remove(index)}>
              <Icon name="close" />
              {text.common.remove}
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
        {words.addRunTime}
      </button>
      {error !== undefined && (
        <p role="alert" className={styles.error}>
          {runTimesMessage(error, text)}
        </p>
      )}
      <p className={styles.note}>{words.gap(MIN_GAP_HOURS)}</p>
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
