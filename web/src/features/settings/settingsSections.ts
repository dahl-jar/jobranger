interface SettingsSection {
  id: string
  locksOffline: boolean
}

export const SETTINGS_SECTIONS = [
  { id: 'search', locksOffline: true },
  { id: 'delivery', locksOffline: true },
  { id: 'appearance', locksOffline: false },
  { id: 'model', locksOffline: true },
  { id: 'matching', locksOffline: false },
  { id: 'connection', locksOffline: false },
  { id: 'profile', locksOffline: true },
] as const satisfies readonly SettingsSection[]

export type SettingsSectionId = (typeof SETTINGS_SECTIONS)[number]['id']
