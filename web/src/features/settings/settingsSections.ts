interface SettingsSection {
  id: string
  label: string
  note: string
  locksOffline: boolean
}

export const SETTINGS_SECTIONS = [
  { id: 'search', label: 'Search', note: 'What counts as a match.', locksOffline: true },
  { id: 'delivery', label: 'Delivery', note: 'How and when you get matches.', locksOffline: true },
  { id: 'appearance', label: 'Appearance', note: 'For this browser.', locksOffline: false },
  { id: 'model', label: 'Model', note: 'Runs from your machine with your own access.', locksOffline: true },
  { id: 'matching', label: 'Matching', note: 'Applies from the next run.', locksOffline: false },
  { id: 'connection', label: 'Connection', note: 'The machine this page is paired with.', locksOffline: false },
  { id: 'profile', label: 'Profile', note: 'What jobranger compares ads against.', locksOffline: true },
] as const satisfies readonly SettingsSection[]

export type SettingsSectionId = (typeof SETTINGS_SECTIONS)[number]['id']
