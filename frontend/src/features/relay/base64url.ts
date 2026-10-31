const URL_ALPHABET = /^[A-Za-z0-9_-]*$/
const BASE64_BLOCK = 4
const INVALID_REMAINDER = 1

export class Base64UrlError extends Error {
  constructor(text: string) {
    super(`not canonical base64url: ${text.slice(0, 16)}`)
    this.name = 'Base64UrlError'
  }
}

export function encodeBase64(bytes: Uint8Array): string {
  return btoa(Array.from(bytes, (byte) => String.fromCharCode(byte)).join(''))
}

export function encodeBase64Url(bytes: Uint8Array): string {
  return encodeBase64(bytes).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
}

export function decodeBase64Url(text: string): Uint8Array<ArrayBuffer> {
  if (!URL_ALPHABET.test(text) || text.length % BASE64_BLOCK === INVALID_REMAINDER) {
    throw new Base64UrlError(text)
  }
  const binary = atob(text.replaceAll('-', '+').replaceAll('_', '/'))
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
  if (encodeBase64Url(bytes) !== text) {
    throw new Base64UrlError(text)
  }
  return bytes
}
