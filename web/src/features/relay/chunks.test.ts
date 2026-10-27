import { describe, expect, it } from 'vitest'
import { encodeBase64 } from '@/features/relay/base64url'
import { ChunkAssembler, parseAssembled, splitMessage } from '@/features/relay/chunks'
import { CHUNK_DATA_MAX_BYTES, CHUNK_MAX_COUNT, CHUNK_THRESHOLD_BYTES, REASSEMBLY_TIMEOUT_SECONDS } from '@/features/relay/contract/limits'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import type { Chunk, CvUpload, Payload } from '@/features/relay/contract/payload.gen'

const CV_UPLOAD = payloadFixtures['cv.upload'] as CvUpload
const BASE_BYTES = JSON.stringify({ ...CV_UPLOAD, content_b64: '' }).length
const HUNDRED_KB = 100 * 1024
const TIMEOUT_MS = REASSEMBLY_TIMEOUT_SECONDS * 1000
const BASE64 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
const SINGLE: Chunk = { type: 'chunk', v: 1, chunk_id: '00000000-0000-4000-8000-000000000099', index: 0, count: 1, data: '' }

function statusPaddedToTwoEquals(): string {
  const text = JSON.stringify(payloadFixtures.status)
  return text + ' '.repeat((1 - (text.length % 3) + 3) % 3)
}

function cvOfBytes(totalBytes: number): CvUpload {
  return { ...CV_UPLOAD, content_b64: 'A'.repeat(totalBytes - BASE_BYTES) }
}

function chunksOf(message: Payload): Chunk[] {
  return splitMessage(message).filter((part): part is Chunk => part.type === 'chunk')
}

function feed(assembler: ChunkAssembler, chunks: Chunk[], nowMs = 0): Payload | undefined {
  let text: string | undefined
  for (const chunk of chunks) text = assembler.acceptText(chunk, nowMs)
  return text === undefined ? undefined : parseAssembled(text)
}

describe('splitMessage', () => {
  it('should keep a message of exactly CHUNK_THRESHOLD_BYTES whole', () => {
    const message = cvOfBytes(CHUNK_THRESHOLD_BYTES)

    expect(splitMessage(message)).toEqual([message])
  })

  it('should chunk a message one byte over CHUNK_THRESHOLD_BYTES', () => {
    expect(chunksOf(cvOfBytes(CHUNK_THRESHOLD_BYTES + 1))).toHaveLength(2)
  })

  it('should count UTF-8 bytes when deciding to chunk', () => {
    const message = { ...cvOfBytes(CHUNK_THRESHOLD_BYTES - 40), name: `${'é'.repeat(56)}.pdf` }

    expect(chunksOf(message)).toHaveLength(2)
  })

  it('should cut a 100 KB message into chunks of at most CHUNK_DATA_MAX_BYTES', () => {
    const chunks = chunksOf(cvOfBytes(HUNDRED_KB))

    expect(chunks.map((chunk) => [chunk.index, chunk.count, atob(chunk.data).length])).toEqual([
      [0, 4, CHUNK_DATA_MAX_BYTES],
      [1, 4, CHUNK_DATA_MAX_BYTES],
      [2, 4, CHUNK_DATA_MAX_BYTES],
      [3, 4, HUNDRED_KB - 3 * CHUNK_DATA_MAX_BYTES],
    ])
  })

  it('should give every chunk of one message the same new chunk id', () => {
    const ids = chunksOf(cvOfBytes(HUNDRED_KB)).map((chunk) => chunk.chunk_id)

    expect([new Set(ids).size, ids[0]]).toEqual([1, expect.stringMatching(/^[0-9a-f-]{36}$/)])
  })

  it('should chunk a message that needs exactly CHUNK_MAX_COUNT chunks', () => {
    expect(chunksOf(cvOfBytes(CHUNK_MAX_COUNT * CHUNK_DATA_MAX_BYTES))).toHaveLength(CHUNK_MAX_COUNT)
  })

  it('should throw when a message needs more than CHUNK_MAX_COUNT chunks', () => {
    expect(() => splitMessage(cvOfBytes(CHUNK_MAX_COUNT * CHUNK_DATA_MAX_BYTES + 1))).toThrow('too large to chunk')
  })
})

describe('ChunkAssembler', () => {
  it('should rebuild a 100 KB message from its chunks in order', () => {
    const message = cvOfBytes(HUNDRED_KB)

    expect(feed(new ChunkAssembler(), chunksOf(message))).toEqual(message)
  })

  it('should hand over the rebuilt JSON text without parsing it', () => {
    const message = cvOfBytes(HUNDRED_KB)
    const assembler = new ChunkAssembler()
    const chunks = chunksOf(message)
    chunks.slice(0, -1).forEach((chunk) => assembler.acceptText(chunk, 0))

    expect(assembler.acceptText(chunks.at(-1) ?? SINGLE, 0)).toBe(JSON.stringify(message))
  })

  it('should return nothing until the last chunk arrives', () => {
    const chunks = chunksOf(cvOfBytes(HUNDRED_KB))

    expect(feed(new ChunkAssembler(), chunks.slice(0, -1))).toBeUndefined()
  })

  it('should drop a set when a chunk arrives out of order', () => {
    const [first, second, third, fourth] = chunksOf(cvOfBytes(HUNDRED_KB))
    const assembler = new ChunkAssembler()

    expect(feed(assembler, [first, third, second, fourth].filter((chunk) => chunk !== undefined))).toBeUndefined()
  })

  it('should drop a set when the count changes', () => {
    const chunks = chunksOf(cvOfBytes(HUNDRED_KB)).map((chunk) => (chunk.index === 3 ? { ...chunk, count: 5 } : chunk))

    expect(feed(new ChunkAssembler(), chunks)).toBeUndefined()
  })

  it('should drop a set when the next chunk comes after REASSEMBLY_TIMEOUT_SECONDS', () => {
    const chunks = chunksOf(cvOfBytes(HUNDRED_KB))
    const assembler = new ChunkAssembler()
    feed(assembler, chunks.slice(0, -1), 0)

    expect(feed(assembler, chunks.slice(-1), TIMEOUT_MS + 1)).toBeUndefined()
  })

  it('should keep a set when the next chunk comes just inside REASSEMBLY_TIMEOUT_SECONDS', () => {
    const message = cvOfBytes(HUNDRED_KB)
    const chunks = chunksOf(message)
    const assembler = new ChunkAssembler()
    feed(assembler, chunks.slice(0, -1), 0)

    expect(feed(assembler, chunks.slice(-1), TIMEOUT_MS)).toEqual(message)
  })

  it('should refuse a third set while two are open', () => {
    const assembler = new ChunkAssembler()
    const sets = [1, 2, 3].map(() => chunksOf(cvOfBytes(HUNDRED_KB)))
    for (const set of sets) feed(assembler, set.slice(0, 1))

    expect(feed(assembler, sets[2]?.slice(1) ?? [])).toBeUndefined()
  })

  it('should open a new set once an earlier one completes', () => {
    const assembler = new ChunkAssembler()
    const message = cvOfBytes(HUNDRED_KB)
    const [open, done, later] = [chunksOf(message), chunksOf(message), chunksOf(message)]
    feed(assembler, open.slice(0, 1))
    feed(assembler, done)

    expect(feed(assembler, later)).toEqual(message)
  })

  it('should drop a rebuilt message that is itself a chunk', () => {
    const inner = payloadFixtures.chunk as Chunk
    const outer: Chunk = { ...inner, chunk_id: '00000000-0000-4000-8000-000000000099', index: 0, count: 1, data: btoa(JSON.stringify(inner)) }

    expect(feed(new ChunkAssembler(), [outer])).toBeUndefined()
  })

  it('should drop data whose padding bits are not zero', () => {
    const canonical = btoa(statusPaddedToTwoEquals())
    const last = canonical.at(-3) ?? 'A'
    const data = `${canonical.slice(0, -3)}${BASE64[BASE64.indexOf(last) + 1] ?? 'B'}==`

    expect(feed(new ChunkAssembler(), [{ ...SINGLE, data }])).toBeUndefined()
  })

  it('should drop a rebuilt message that is not UTF-8', () => {
    const bytes = new TextEncoder().encode(JSON.stringify({ ...CV_UPLOAD, name: 'Xcv.pdf' }))
    bytes[bytes.indexOf(0x58)] = 0xff

    expect(feed(new ChunkAssembler(), [{ ...SINGLE, data: encodeBase64(bytes) }])).toBeUndefined()
  })

  it('should drop a set that does not start at index 0', () => {
    expect(feed(new ChunkAssembler(), [{ ...SINGLE, index: 1, data: btoa(statusPaddedToTwoEquals()) }])).toBeUndefined()
  })

  it('should not resume a set after an out-of-order chunk', () => {
    const [first, second, third, fourth] = chunksOf(cvOfBytes(HUNDRED_KB))
    const order = [first, third, second, third, fourth].filter((chunk) => chunk !== undefined)

    expect(feed(new ChunkAssembler(), order)).toBeUndefined()
  })

  it('should time a set from its last chunk', () => {
    const message = cvOfBytes(HUNDRED_KB)
    const chunks = chunksOf(message)
    const assembler = new ChunkAssembler()
    feed(assembler, chunks.slice(0, 1), 0)
    feed(assembler, chunks.slice(1, -1), TIMEOUT_MS)

    expect(feed(assembler, chunks.slice(-1), 2 * TIMEOUT_MS)).toEqual(message)
  })

  it('should rebuild a single chunk carrying a whole message', () => {
    expect(feed(new ChunkAssembler(), [{ ...SINGLE, data: btoa(statusPaddedToTwoEquals()) }])).toEqual(payloadFixtures.status)
  })

  it('should drop a set whose data is not canonical base64', () => {
    const chunks = chunksOf(cvOfBytes(HUNDRED_KB)).map((chunk) => (chunk.index === 1 ? { ...chunk, data: `${chunk.data}==` } : chunk))

    expect(feed(new ChunkAssembler(), chunks)).toBeUndefined()
  })
})
