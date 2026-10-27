import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { commandErrorText } from '@/features/shell/commandErrorText'
import { useSnapshot } from '@/app/useSnapshot'
import type { CommandDraft, CommandError } from '@/features/relay/commands'
import type {
  ConfigPatch,
  ContextRemove,
  ContextUpdate,
  ContextUpload,
  CvUpload as CvUploadCommand,
  ErrorMessage,
  GuidanceReset,
  GuidanceUpdate,
  KeyReplace,
  ModelSelect,
  ProfileUpdate,
  Saved,
} from '@/features/relay/contract/payload.gen'
import { machineToday } from '@/app/machineToday'
import { useConnection, useRelay } from '@/features/relay/relayContext'
import { useCommand } from '@/features/relay/useCommand'
import { AboutYou } from '@/features/settings/AboutYou'
import { AppearanceSettings } from '@/features/settings/AppearanceSettings'
import { ConnectionSettings } from '@/features/settings/ConnectionSettings'
import { ContextDocuments } from '@/features/settings/ContextDocuments'
import { CvUpload } from '@/features/settings/CvUpload'
import { DeliverySettings } from '@/features/settings/DeliverySettings'
import { MatchingSettings } from '@/features/settings/MatchingSettings'
import { ModelSettings } from '@/features/settings/ModelSettings'
import { ProfileSettings } from '@/features/settings/ProfileSettings'
import type { SaveResult } from '@/features/settings/SavedLine'
import { SearchSettings } from '@/features/settings/SearchSettings'
import { SettingsLayout } from '@/features/settings/SettingsLayout'
import type { SettingsSectionId } from '@/features/settings/settingsSections'
import styles from '@/features/settings/SettingsPage.module.css'
import { useConfigPatch } from '@/features/settings/useConfigPatch'
import { useEditsDisabled } from '@/features/shell/useEditsDisabled'
import { UsageTable } from '@/features/usage/UsageTable'
import { useText } from '@/shared/i18n/languageContext'
import { usePageTitle } from '@/shared/usePageTitle'

type Draft<Command> = Omit<Command, 'id'>

const LOCAL_ERROR_CODES: ReadonlySet<CommandError['code']> = new Set(['timeout', 'disconnected'])

interface SaveHandlers {
  onSaved: (saved: Saved) => void
  onFailed: (error: CommandError) => void
}

function replyCode(error: CommandError | null): ErrorMessage['code'] | undefined {
  if (error === null || LOCAL_ERROR_CODES.has(error.code)) return undefined
  return error.code as ErrorMessage['code']
}

function useStoragePersisted(): boolean {
  const [isPersisted, setPersisted] = useState(true)

  useEffect(() => {
    let isCurrent = true
    navigator.storage
      ?.persisted?.()
      .then((persisted) => {
        if (isCurrent) setPersisted(persisted)
      })
      .catch(() => undefined)
    return () => {
      isCurrent = false
    }
  }, [])

  return isPersisted
}

function useSaveResult(section: string | undefined) {
  const text = useText()
  const [shown, setShown] = useState<{ section: string | undefined; result: SaveResult } | undefined>(undefined)
  const handlers: SaveHandlers = {
    onSaved: (saved) => setShown({ section, result: { kind: 'saved', recheck: saved.recheck } }),
    onFailed: (error) => setShown({ section, result: { kind: 'failed', message: commandErrorText(error, text) } }),
  }
  return { result: shown?.section === section ? shown?.result : undefined, handlers }
}

function useSectionCommand<Command extends CommandDraft>(handlers: SaveHandlers) {
  return useCommand<Command>({ onSuccess: handlers.onSaved, onError: handlers.onFailed })
}

function ProfileSection({ handlers }: { handlers: SaveHandlers }) {
  const profile = useSnapshot('profile')
  const context = useSnapshot('context')
  const today = useSnapshot('ads')?.as_of
  const cv = useSectionCommand<Draft<CvUploadCommand>>(handlers)
  const update = useSectionCommand<Draft<ProfileUpdate>>(handlers)
  const about = useSectionCommand<Draft<ContextUpdate>>(handlers)
  const upload = useSectionCommand<Draft<ContextUpload>>(handlers)
  const remove = useSectionCommand<Draft<ContextRemove>>(handlers)
  if (profile === undefined || today === undefined) return null

  return (
    <ProfileSettings
      profile={profile}
      onUpdate={(patch) => update.mutate({ type: 'profile.update', v: 1, ...patch })}
      cvUpload={
        <CvUpload
          cvName={profile.cv_name}
          cvReadAt={profile.cv_read_at}
          today={today}
          progress={cv.progress}
          errorCode={replyCode(cv.error)}
          onUpload={(file) => cv.mutate({ type: 'cv.upload', v: 1, name: file.name, size_bytes: file.sizeBytes, content_b64: file.contentB64 })}
        />
      }
    >
      {context !== undefined && (
        <>
          <AboutYou context={context} errorCode={replyCode(about.error)} onSave={(text) => about.mutate({ type: 'context.update', v: 1, about: text })} />
          <ContextDocuments
            context={context}
            today={today}
            progress={upload.progress}
            errorCode={replyCode(upload.error ?? remove.error)}
            onUpload={(file) => {
              remove.reset()
              upload.mutate({ type: 'context.upload', v: 1, name: file.name, content_b64: file.contentB64 })
            }}
            onRemove={(docId) => {
              upload.reset()
              remove.mutate({ type: 'context.remove', v: 1, doc_id: docId })
            }}
          />
        </>
      )}
    </ProfileSettings>
  )
}

function ModelSection({ handlers, onPatch }: { handlers: SaveHandlers; onPatch: (patch: ConfigPatch) => void }) {
  const config = useSnapshot('config')
  const models = useSnapshot('models')
  const usage = useSnapshot('usage')
  const ads = useSnapshot('ads')
  const select = useSectionCommand<Draft<ModelSelect>>(handlers)
  const key = useSectionCommand<Draft<KeyReplace>>(handlers)
  if (config === undefined || models === undefined || usage === undefined || ads === undefined) return null

  return (
    <ModelSettings
      config={config}
      models={models}
      usage={usage}
      usageTable={<UsageTable usage={usage} today={machineToday(ads, usage)} />}
      onSelectModel={(provider, model) => select.mutate({ type: 'model.select', v: 1, provider, model })}
      onReplaceKey={(provider, value) => key.mutate({ type: 'key.replace', v: 1, provider, key: value })}
      onPatch={onPatch}
    />
  )
}

function MatchingSection({ handlers }: { handlers: SaveHandlers }) {
  const guidance = useSnapshot('guidance')
  const editsDisabled = useEditsDisabled()
  const save = useSectionCommand<Draft<GuidanceUpdate>>(handlers)
  const reset = useSectionCommand<Draft<GuidanceReset>>(handlers)
  if (guidance === undefined) return null

  return (
    <MatchingSettings
      guidance={guidance}
      errorCode={replyCode(save.error ?? reset.error)}
      editsDisabled={editsDisabled || save.isPending || reset.isPending}
      onSave={(text) => {
        reset.reset()
        save.mutate({ type: 'guidance.update', v: 1, text })
      }}
      onReset={() => {
        save.reset()
        reset.mutate({ type: 'guidance.reset', v: 1 })
      }}
    />
  )
}

function ConnectionSection() {
  const status = useSnapshot('status')
  const today = useSnapshot('ads')?.as_of
  const isOnline = useConnection().kind === 'ready'
  const isPersisted = useStoragePersisted()
  const relay = useRelay()
  if (status === undefined || today === undefined) return null

  return <ConnectionSettings status={status} isOnline={isOnline} isPersisted={isPersisted} today={today} onUnpair={() => void relay.unpair()} />
}

function SettingsSection({ section, handlers }: { section: SettingsSectionId; handlers: SaveHandlers }) {
  const config = useSnapshot('config')
  const { patch } = useConfigPatch(handlers)

  switch (section) {
    case 'search':
      return config && <SearchSettings config={config} onPatch={patch} />
    case 'delivery':
      return config && <DeliverySettings config={config} onPatch={patch} />
    case 'appearance':
      return <AppearanceSettings onPatch={patch} />
    case 'model':
      return <ModelSection handlers={handlers} onPatch={patch} />
    case 'matching':
      return <MatchingSection handlers={handlers} />
    case 'connection':
      return <ConnectionSection />
    case 'profile':
      return <ProfileSection handlers={handlers} />
  }
}

export function SettingsPage() {
  const { section } = useParams()
  const editsDisabled = useEditsDisabled()
  const words = useText().settings
  const { result, handlers } = useSaveResult(section)
  usePageTitle(words.nav)

  return (
    <main className={styles.page}>
      <h1 className="visually-hidden">{words.nav}</h1>
      <SettingsLayout editsDisabled={editsDisabled} saveResult={result} renderSection={(current) => <SettingsSection section={current} handlers={handlers} />} />
    </main>
  )
}
