import { describe, expect, it, vi } from 'vitest'
import { CV_MAX_BYTES } from '@/features/relay/contract/limits'
import { CV_BYTES, CV_UPLOAD, decodeBase64 } from '@/features/settings/settings.samples'
import { readUploadFile } from '@/features/settings/uploadFile'

const LONGER_THAN_ONE_CHUNK = 0x8000 + 3

describe('readUploadFile', () => {
  it('should read the file as base64 with its name and size', async () => {
    const result = await readUploadFile(new File([CV_BYTES], CV_UPLOAD.name))

    expect(result).toEqual({
      kind: 'ok',
      file: { name: CV_UPLOAD.name, sizeBytes: CV_UPLOAD.size_bytes, contentB64: CV_UPLOAD.content_b64 },
    })
  })

  it.each(['notes.TXT', 'portfolio.md'])('should accept %s', async (name) => {
    const result = await readUploadFile(new File([CV_BYTES], name))

    expect(result.kind).toBe('ok')
  })

  it.each(['cv.docx', 'cv.pdf.exe', 'cv'])('should refuse %s by type', async (name) => {
    const result = await readUploadFile(new File([CV_BYTES], name))

    expect(result).toEqual({ kind: 'error', error: 'type' })
  })

  it('should refuse an empty file', async () => {
    const result = await readUploadFile(new File([], 'cv.pdf'))

    expect(result).toEqual({ kind: 'error', error: 'empty' })
  })

  it.each([
    { name: `${'a'.repeat(117)}.pdf`, why: 'too long' },
    { name: 'cv\u202Efdp.txt', why: 'a bidi control' },
  ])('should refuse a file name with $why', async ({ name }) => {
    const result = await readUploadFile(new File([CV_BYTES], name))

    expect(result).toEqual({ kind: 'error', error: 'name' })
  })

  it('should accept a name of exactly 120 characters', async () => {
    const result = await readUploadFile(new File([CV_BYTES], `${'a'.repeat(116)}.pdf`))

    expect(result.kind).toBe('ok')
  })

  it('should accept a file of exactly 5 MB', async () => {
    const result = await readUploadFile(new File([new Uint8Array(CV_MAX_BYTES)], 'cv.pdf'))

    expect(result.kind).toBe('ok')
  })

  it('should refuse a file one byte over 5 MB', async () => {
    const result = await readUploadFile(new File([new Uint8Array(CV_MAX_BYTES + 1)], 'cv.pdf'))

    expect(result).toEqual({ kind: 'error', error: 'tooLarge' })
  })

  it('should read a file longer than one chunk', async () => {
    const bytes = Uint8Array.from({ length: LONGER_THAN_ONE_CHUNK }, (_, index) => index % 256)

    const result = await readUploadFile(new File([bytes], 'cv.pdf'))

    expect(result.kind === 'ok' && decodeBase64(result.file.contentB64)).toEqual(bytes)
  })

  it('should report unreadable when the browser cannot read the file', async () => {
    const file = new File([CV_BYTES], 'cv.pdf')
    vi.spyOn(file, 'arrayBuffer').mockRejectedValue(new DOMException('gone', 'NotReadableError'))

    const result = await readUploadFile(file)

    expect(result).toEqual({ kind: 'error', error: 'unreadable' })
  })
})
