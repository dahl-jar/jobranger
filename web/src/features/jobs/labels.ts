import type { JobList } from '@/features/jobs/selectors'
import type {
  ClosedAd,
  ClosedMark,
  Level,
  Mark,
  RoleOption,
  RoleType,
  SourceId,
  YearBand,
} from '@/features/relay/contract/payload.gen'

export const SOURCE_NAMES: Record<SourceId, string> = {
  nav: 'NAV',
  finn: 'FINN',
  linkedin: 'LinkedIn',
  teamtailor: 'Teamtailor',
  ashby: 'Ashby',
}

export const MARK_LABELS: Record<Mark, string> = {
  applied: 'Applied',
  skipped: 'Skipped',
  not_for_me: 'Not for me',
}

export const CLOSED_MARK_LABELS: Record<ClosedMark, string> = {
  ...MARK_LABELS,
  none: 'No action',
}

export const LIST_LABELS: Record<JobList, string> = {
  today: 'Today',
  active: 'Active',
  hidden: 'Hidden',
  closed: 'Closed',
  all: 'All',
}

export const LIST_NOTES: Partial<Record<JobList, string>> = {
  active: 'Open matches, soonest deadline first.',
  hidden: 'Filtered out by your limits.',
  closed: 'Deadline passed or ad removed.',
  all: 'Every ad jobranger has checked for you.',
}

export const LEVELS: readonly Level[] = ['junior', 'mid', 'senior', 'lead', 'none']

export const LEVEL_LABELS: Record<Level, string> = {
  junior: 'Junior',
  mid: 'Mid-level',
  senior: 'Senior',
  lead: 'Tech lead',
  none: 'No level stated',
}

export const YEAR_BANDS: readonly YearBand[] = ['0-1', '1-3', '3-6', '6+']

export const YEAR_BAND_LABELS: Record<YearBand, string> = {
  '0-1': '0–1 years',
  '1-3': '1–3 years',
  '3-6': '3–6 years',
  '6+': '6+ years',
}

const GENERAL_SOFTWARE_LABEL = 'Software'
const OTHER_ROLE_LABEL = 'Other'

export function closedWhy(ad: ClosedAd): string {
  switch (ad.reason) {
    case 'deadline':
      return 'Deadline passed'
    case 'removed':
      return `Removed from ${SOURCE_NAMES[ad.source]}`
    case 'filled':
      return 'Filled, ad says so'
  }
}

export function roleLabel(roleType: RoleType | null, roles: readonly RoleOption[]): string {
  if (roleType === 'general_software') return GENERAL_SOFTWARE_LABEL
  return roles.find((role) => role.id === roleType)?.label ?? OTHER_ROLE_LABEL
}

export function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}
