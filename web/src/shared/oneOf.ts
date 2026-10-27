export function oneOf<T extends string>(values: readonly T[], value: string | null | undefined): T | undefined {
  return values.find((item) => item === value)
}
