import type { Application, ApplicationStatus } from '@/features/relay/contract/payload.gen'
import { oneOf } from '@/shared/oneOf'

export const APPLICATION_STATUSES: readonly ApplicationStatus[] = ['waiting', 'interview', 'offer', 'accepted', 'rejected']

export function parseStatusFilter(param: string | null): ApplicationStatus | undefined {
  return oneOf(APPLICATION_STATUSES, param)
}

export function countStatuses(applications: readonly Application[]): Record<ApplicationStatus, number> {
  const counts: Record<ApplicationStatus, number> = { waiting: 0, interview: 0, offer: 0, accepted: 0, rejected: 0 }
  return applications.reduce((total, application) => ({ ...total, [application.status]: total[application.status] + 1 }), counts)
}

export function listedApplications(applications: readonly Application[], hiddenId?: number): Application[] {
  return applications.filter((application) => !application.removed && application.id !== hiddenId)
}
