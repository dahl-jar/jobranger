import { CommandError } from '@/services/relayCommands/commandTracker'
import type { Messages } from '@/i18n/en'

export function describeCommandError(error: unknown, text: Messages): string {
  if (error instanceof CommandError && (error.code === 'timeout' || error.code === 'disconnected')) return text.connection.commandErrors[error.code]
  return error instanceof Error ? error.message : String(error)
}
