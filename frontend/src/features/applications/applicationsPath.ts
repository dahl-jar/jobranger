export const FROM_LIST_STATE = { fromList: true } as const

export function applicationsPath(base: string): string {
  return `${base}/applications`
}

export function applicationPath(applicationId: number, search: string, base: string): string {
  return `${applicationsPath(base)}/${applicationId}${search}`
}

export function openedFromList(state: unknown): boolean {
  return typeof state === 'object' && state !== null && 'fromList' in state
}
