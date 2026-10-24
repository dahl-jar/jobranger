import { existsSync } from 'node:fs'
import { dirname, join } from 'node:path'

const CONTRACTS_DIR = 'contracts'
const CONTRACTS_MARKER = join(CONTRACTS_DIR, 'payload.schema.json')

export function findContracts(start: string): string {
  let folder = start
  while (!existsSync(join(folder, CONTRACTS_MARKER))) {
    const parent = dirname(folder)
    if (parent === folder) {
      throw new Error(`no folder above ${start} holds ${CONTRACTS_MARKER}`)
    }
    folder = parent
  }
  return join(folder, CONTRACTS_DIR)
}
