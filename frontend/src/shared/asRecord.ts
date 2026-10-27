export function asRecord(value: unknown): Record<string, unknown> | undefined {
  if (typeof value !== 'object' || value === null) return undefined
  return Object.fromEntries(Object.entries(value))
}
