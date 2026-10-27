export const APPLICATIONS_PATH = '/app/applications'

export const FROM_LIST_STATE = { fromList: true } as const

export function applicationPath(applicationId: number, search: string): string {
  return `${APPLICATIONS_PATH}/${applicationId}${search}`
}

export function openedFromList(state: unknown): boolean {
  return typeof state === 'object' && state !== null && 'fromList' in state
}
