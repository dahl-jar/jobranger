import { EFF_SHORT_WORDLIST, WORDLIST_SIZE } from '@/features/pairing/effShortWordlist'

const SAS_BYTES = 8
const WORD_COUNT = 4
const BASE = BigInt(WORDLIST_SIZE)

export function confirmationWords(sas: Uint8Array): string[] {
  if (sas.length !== SAS_BYTES) throw new Error(`sas must be ${SAS_BYTES} bytes, got ${sas.length}`)
  let n = new DataView(sas.buffer, sas.byteOffset, SAS_BYTES).getBigUint64(0)
  const words: string[] = []
  for (let index = 0; index < WORD_COUNT; index += 1) {
    words.push(EFF_SHORT_WORDLIST[Number(n % BASE)] ?? '')
    n /= BASE
  }
  return words
}
