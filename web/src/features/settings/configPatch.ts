import type { ConfigPatch, ConfigSnapshot } from '@/features/relay/contract/payload.gen'

export function applyConfigPatch(config: ConfigSnapshot, patch: ConfigPatch): ConfigSnapshot {
  return {
    ...config,
    targets: { ...config.targets, ...patch.targets },
    sources: { ...config.sources, ...patch.sources },
    schedule: { ...config.schedule, ...patch.schedule },
    notify: { ...config.notify, ...patch.notify, email: { ...config.notify.email, ...patch.notify?.email } },
    model: { ...config.model, ...patch.model, price: { ...config.model.price, ...patch.model?.price } },
    language: patch.language ?? config.language,
  }
}
