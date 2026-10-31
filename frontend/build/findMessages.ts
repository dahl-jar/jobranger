import { existsSync } from 'node:fs'
import { dirname, join } from 'node:path'

const MESSAGES_DIR = 'messages'
const MESSAGES_MARKER = join(MESSAGES_DIR, 'payload.schema.json')

export function findMessages(start: string): string {
  let folder = start
  while (!existsSync(join(folder, MESSAGES_MARKER))) {
    const parent = dirname(folder)
    if (parent === folder) {
      throw new Error(`no folder above ${start} holds ${MESSAGES_MARKER}`)
    }
    folder = parent
  }
  return join(folder, MESSAGES_DIR)
}
