import wordlistText from '@contracts/crypto/eff_short_wordlist_2.txt?raw'

export const WORDLIST_SIZE = 1296

const words = wordlistText.split('\n').filter((word) => word !== '')
if (words.length !== WORDLIST_SIZE) {
  throw new Error(`EFF short wordlist 2 has ${words.length} words, expected ${WORDLIST_SIZE}`)
}

export const EFF_SHORT_WORDLIST: readonly string[] = words
