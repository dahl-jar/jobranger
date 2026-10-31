import { CommandError } from '@/features/relay/commands'
import type { Messages } from '@/shared/i18n/en'

export function commandErrorText(error: unknown, text: Messages): string {
  if (error instanceof CommandError && (error.code === 'timeout' || error.code === 'disconnected')) return text.connection.commandErrors[error.code]
  return error instanceof Error ? error.message : String(error)
}
