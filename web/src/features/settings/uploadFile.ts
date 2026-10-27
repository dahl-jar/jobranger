import { CV_MAX_BYTES } from '@/features/relay/contract/limits'
import { payloadSchema } from '@/features/relay/contract/schemas.gen'

export const UPLOAD_ACCEPT = '.pdf,.txt,.md'

const FILE_NAME = payloadSchema.$defs.FileName
export const FILE_NAME_MAX_CHARS = FILE_NAME.maxLength
const UPLOAD_NAME_PATTERN = new RegExp(FILE_NAME.pattern, 'u')
const FORBIDDEN_NAME_CHARACTERS = new RegExp(FILE_NAME.not.pattern, 'u')
const BASE64_CHUNK_BYTES = 0x8000

export interface UploadFile {
  name: string
  sizeBytes: number
  contentB64: string
}

export type UploadReadError = 'type' | 'name' | 'empty' | 'tooLarge' | 'unreadable'

type UploadReadResult = { kind: 'ok'; file: UploadFile } | { kind: 'error'; error: UploadReadError }

function encodeBase64(bytes: Uint8Array): string {
  const parts: string[] = []
  for (let start = 0; start < bytes.length; start += BASE64_CHUNK_BYTES) {
    parts.push(String.fromCharCode(...bytes.subarray(start, start + BASE64_CHUNK_BYTES)))
  }
  return btoa(parts.join(''))
}

export async function readUploadFile(file: File): Promise<UploadReadResult> {
  if (!UPLOAD_NAME_PATTERN.test(file.name)) return { kind: 'error', error: 'type' }
  if (file.name.length > FILE_NAME_MAX_CHARS || FORBIDDEN_NAME_CHARACTERS.test(file.name)) return { kind: 'error', error: 'name' }
  if (file.size === 0) return { kind: 'error', error: 'empty' }
  if (file.size > CV_MAX_BYTES) return { kind: 'error', error: 'tooLarge' }
  let buffer: ArrayBuffer
  try {
    buffer = await file.arrayBuffer()
  } catch {
    return { kind: 'error', error: 'unreadable' }
  }
  return { kind: 'ok', file: { name: file.name, sizeBytes: file.size, contentB64: encodeBase64(new Uint8Array(buffer)) } }
}
