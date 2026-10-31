import { vi } from 'vitest'
import type { SnapshotData } from '@/app/useSnapshot'
import { CommandError, type CommandDraft } from '@/features/relay/commands'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type {
  AdsSnapshot,
  ApplicationsSnapshot,
  ConfigSnapshot,
  ContextSnapshot,
  ErrorMessage,
  GuidanceSnapshot,
  ModelsSnapshot,
  ProfileSnapshot,
  Saved,
  Status,
  UsageSnapshot,
} from '@/features/relay/contract/payload.gen'
import type { SendProgress } from '@/features/relay/sessionRunner'

export const SAVED = payloadFixtures.saved as Saved
export const REFUSED = payloadFixtures.error as ErrorMessage

export const SNAPSHOTS: SnapshotData = {
  status: payloadFixtures['status.idle'] as Status,
  ads: payloadFixtures['ads.snapshot'] as AdsSnapshot,
  applications: payloadFixtures['applications.snapshot'] as ApplicationsSnapshot,
  config: payloadFixtures['config.snapshot'] as ConfigSnapshot,
  profile: payloadFixtures['profile.snapshot'] as ProfileSnapshot,
  context: payloadFixtures['context.snapshot'] as ContextSnapshot,
  guidance: payloadFixtures['guidance.snapshot'] as GuidanceSnapshot,
  usage: payloadFixtures['usage.snapshot'] as UsageSnapshot,
  models: payloadFixtures['models.snapshot'] as ModelsSnapshot,
}

export type SendMock = ReturnType<typeof vi.fn<(draft: CommandDraft, onProgress?: SendProgress) => Promise<Saved>>>

export function refusal(code: ErrorMessage['code'] = REFUSED.code, message = REFUSED.message): CommandError {
  return new CommandError(code, message, REFUSED.field)
}

export function heldSend() {
  let settle: { resolve: (saved: Saved) => void; reject: (error: CommandError) => void } | undefined
  const send: SendMock = vi.fn(
    () =>
      new Promise<Saved>((resolve, reject) => {
        settle = { resolve, reject }
      }),
  )
  return {
    send,
    resolve: (saved: Saved = SAVED) => settle?.resolve(saved),
    reject: (error: CommandError = refusal()) => settle?.reject(error),
  }
}

export function sentDrafts(send: SendMock): CommandDraft[] {
  return send.mock.calls.map(([draft]) => draft)
}
