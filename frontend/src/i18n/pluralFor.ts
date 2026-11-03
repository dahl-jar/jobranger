type Plural = (count: number, one: string, other: string) => string

export function pluralFor(locale: string): Plural {
  const rules = new Intl.PluralRules(locale)
  return (count, one, other) => (rules.select(count) === 'one' ? one : other)
}
