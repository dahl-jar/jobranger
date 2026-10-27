const THOUSAND = 1_000
const MILLION = 1_000_000
const BILLION = 1_000_000_000
const TENTHS = 10

function inTenths(count: number, unit: number): number {
  return Math.round((count * TENTHS) / unit) / TENTHS
}

export function formatTokens(count: number): string {
  if (!Number.isFinite(count)) {
    return '0'
  }
  if (count < THOUSAND) {
    return String(count)
  }
  const thousands = Math.round(count / THOUSAND)
  if (thousands < THOUSAND) {
    return `${thousands}k`
  }
  const millions = inTenths(count, MILLION)
  if (millions < THOUSAND) {
    return `${millions}M`
  }
  return `${inTenths(count, BILLION)}B`
}
