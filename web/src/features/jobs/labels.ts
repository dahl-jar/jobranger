import type { ClosedAd, RoleOption, RoleType } from '@/features/relay/contract/payload.gen'
import { en, type Messages } from '@/shared/i18n/en'
import { SOURCE_NAMES } from '@/shared/labels/sources'

export function closedWhy(ad: ClosedAd, text: Messages = en): string {
  const why = text.jobs.closedWhy
  switch (ad.reason) {
    case 'deadline':
      return why.deadline
    case 'removed':
      return why.removed(SOURCE_NAMES[ad.source])
    case 'filled':
      return why.filled
  }
}

export function roleLabel(roleType: RoleType | null, roles: readonly RoleOption[], text: Messages = en): string {
  if (roleType === 'general_software') return text.jobs.generalSoftware
  return roles.find((role) => role.id === roleType)?.label ?? text.jobs.otherRole
}

export function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}
