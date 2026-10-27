import {
  CHUNK_DATA_MAX_BYTES,
  CHUNK_MAX_COUNT,
  CHUNK_THRESHOLD_BYTES,
  CONTRACT_VERSION,
  REASSEMBLY_MAX_OPEN,
  REASSEMBLY_TIMEOUT_SECONDS,
} from '@/features/relay/contract/limits'
import { encodeBase64 } from '@/features/relay/base64url'
import { concatBytes } from '@/features/relay/bytes'
import type { Chunk, Payload } from '@/features/relay/contract/payload.gen'
import { parsePayload } from '@/features/relay/contract/validate'

const MS_PER_SECOND = 1000
const REASSEMBLY_TIMEOUT_MS = REASSEMBLY_TIMEOUT_SECONDS * MS_PER_SECOND

interface OpenSet {
  count: number
  parts: Uint8Array[]
  lastSeenMs: number
}

function fromBase64(text: string): Uint8Array | undefined {
  let binary: string
  try {
    binary = atob(text)
  } catch {
    return undefined
  }
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
  return encodeBase64(bytes) === text ? bytes : undefined
}

export function splitMessage(message: Payload): Payload[] {
  const bytes = new TextEncoder().encode(JSON.stringify(message))
  if (bytes.length <= CHUNK_THRESHOLD_BYTES) return [message]
  const count = Math.ceil(bytes.length / CHUNK_DATA_MAX_BYTES)
  if (count > CHUNK_MAX_COUNT) throw new Error(`message of ${bytes.length} bytes is too large to chunk`)
  const chunkId = crypto.randomUUID()
  return Array.from({ length: count }, (_, index) => ({
    type: 'chunk',
    v: CONTRACT_VERSION,
    chunk_id: chunkId,
    index,
    count,
    data: encodeBase64(bytes.subarray(index * CHUNK_DATA_MAX_BYTES, (index + 1) * CHUNK_DATA_MAX_BYTES)),
  }))
}

function rebuild(parts: Uint8Array[]): Payload | undefined {
  try {
    const message = parsePayload(new TextDecoder('utf-8', { fatal: true }).decode(concatBytes(...parts)))
    return message.type === 'chunk' ? undefined : message
  } catch {
    return undefined
  }
}

export class ChunkAssembler {
  readonly #sets = new Map<string, OpenSet>()

  #prune(nowMs: number): void {
    for (const [chunkId, set] of this.#sets) {
      if (nowMs - set.lastSeenMs > REASSEMBLY_TIMEOUT_MS) this.#sets.delete(chunkId)
    }
  }

  #open(chunk: Chunk, nowMs: number): OpenSet | undefined {
    const existing = this.#sets.get(chunk.chunk_id)
    if (existing) return existing.count === chunk.count && existing.parts.length === chunk.index ? existing : undefined
    if (chunk.index !== 0 || this.#sets.size >= REASSEMBLY_MAX_OPEN) return undefined
    const created: OpenSet = { count: chunk.count, parts: [], lastSeenMs: nowMs }
    this.#sets.set(chunk.chunk_id, created)
    return created
  }

  accept(chunk: Chunk, nowMs: number): Payload | undefined {
    this.#prune(nowMs)
    const set = this.#open(chunk, nowMs)
    const bytes = fromBase64(chunk.data)
    if (!set || !bytes) {
      this.#sets.delete(chunk.chunk_id)
      return undefined
    }
    set.parts.push(bytes)
    set.lastSeenMs = nowMs
    if (set.parts.length < set.count) return undefined
    this.#sets.delete(chunk.chunk_id)
    return rebuild(set.parts)
  }
}
