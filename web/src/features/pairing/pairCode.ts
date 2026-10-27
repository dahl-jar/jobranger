const PAIR_CODE_ALPHABET = 'BCDFGHJKLMNPQRSTVWXZ'
export const PAIR_CODE_LENGTH = 8

const HALF = PAIR_CODE_LENGTH / 2
const SEPARATORS = /[\s-]/g
const CODE_PATTERN = new RegExp(`^[${PAIR_CODE_ALPHABET}]{${PAIR_CODE_LENGTH}}$`)

export type PairCodeError = 'empty' | 'invalid'

export function normalizePairCode(raw: string): string {
  return raw.replace(SEPARATORS, '').toUpperCase()
}

export function validatePairCode(code: string): PairCodeError | undefined {
  if (code === '') return 'empty'
  return CODE_PATTERN.test(code) ? undefined : 'invalid'
}

export function formatPairCode(code: string): string {
  return `${code.slice(0, HALF)}-${code.slice(HALF)}`
}
