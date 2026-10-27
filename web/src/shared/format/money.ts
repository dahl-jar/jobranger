const SMALLEST_SHOWN = 0.01
const BELOW_SMALLEST = '<$0.01'

const USD = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function formatUsd(value: number): string {
  if (!Number.isFinite(value)) {
    return USD.format(0)
  }
  if (value > 0 && value < SMALLEST_SHOWN) {
    return BELOW_SMALLEST
  }
  return USD.format(value)
}
