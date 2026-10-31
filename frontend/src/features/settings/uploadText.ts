import { CV_MAX_BYTES } from '@/features/relay/contract/limits'
import { FILE_NAME_MAX_CHARS, type UploadReadError } from '@/features/settings/uploadFile'
import type { Messages } from '@/shared/i18n/en'

const BYTES_PER_MB = 1048576
const MAX_UPLOAD_MB = CV_MAX_BYTES / BYTES_PER_MB

export function readErrorText(error: UploadReadError, subject: 'CV' | 'file', text: Messages): string {
  const words = text.settings.upload
  switch (error) {
    case 'type':
      return words.wrongType
    case 'name':
      return words.rename(FILE_NAME_MAX_CHARS)
    case 'empty':
      return words.empty
    case 'tooLarge':
      return subject === 'CV' ? words.cvTooLarge(MAX_UPLOAD_MB) : words.fileTooLarge(MAX_UPLOAD_MB)
    case 'unreadable':
      return words.unreadable
  }
}
