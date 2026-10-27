import { CV_MAX_BYTES } from '@/features/relay/contract/limits'
import { FILE_NAME_MAX_CHARS, type UploadReadError } from '@/features/settings/uploadFile'

const BYTES_PER_MB = 1048576
const MAX_UPLOAD_MB = CV_MAX_BYTES / BYTES_PER_MB

export const WRONG_TYPE = 'Use a PDF, text or Markdown file.'
export const UNREADABLE = "jobranger couldn't read this file. Try a PDF or text file."
export const CONTEXT_FULL = 'Your notes and documents are full. Remove a document first.'

export function readErrorText(error: UploadReadError, subject: 'CV' | 'file'): string {
  switch (error) {
    case 'type':
      return WRONG_TYPE
    case 'name':
      return `Rename the file to under ${FILE_NAME_MAX_CHARS} characters, without special characters.`
    case 'empty':
      return 'This file is empty.'
    case 'tooLarge':
      return `The ${subject} must be under ${MAX_UPLOAD_MB} MB.`
    case 'unreadable':
      return UNREADABLE
  }
}
