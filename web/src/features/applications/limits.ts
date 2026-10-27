import { payloadSchema } from '@/features/relay/contract/schemas.gen'

export const COMPANY_MAX_CHARS = payloadSchema.$defs.Company.maxLength
export const TITLE_MAX_CHARS = payloadSchema.$defs.ApplicationCreate.properties.title.maxLength
export const NOTE_MAX_CHARS = payloadSchema.$defs.Note.maxLength
