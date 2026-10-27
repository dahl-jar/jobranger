import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'

export const TODAY = '2026-10-12'
export const EVERY_TWO_HOURS = Array.from({ length: 12 }, (_, index) => `${String(index * 2).padStart(2, '0')}:00`)

export function decodeBase64(text: string): Uint8Array<ArrayBuffer> {
  return Uint8Array.from(atob(text), (char) => char.charCodeAt(0))
}

export const CV_UPLOAD = payloadFixtures['cv.upload']
export const CV_BYTES = decodeBase64(CV_UPLOAD.content_b64)
