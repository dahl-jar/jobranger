import type { FailReason } from '@/features/relay/sessionRunner'

export const FAILURE_TEXT: Record<FailReason, string> = {
  handshake: 'The connection check failed',
  cli_key: 'The connection check failed',
  unknown_device: "Your machine doesn't know this browser",
}
