import { parseAssembled } from '@/features/relay/chunks'
import type { Payload } from '@/features/relay/contract/payload.gen'

export interface ParseRequest {
  id: number
  text: string
}

export interface ParseReply {
  id: number
  payload: Payload | undefined
}

export interface ParserWorker {
  postMessage(request: ParseRequest): void
  onmessage: ((event: MessageEvent<ParseReply>) => void) | null
  onerror: ((event: ErrorEvent) => void) | null
  terminate(): void
}

interface Waiting {
  text: string
  resolve: (payload: Payload | undefined) => void
}

export function parseRequest(request: ParseRequest): ParseReply {
  return { id: request.id, payload: parseAssembled(request.text) }
}

export function createWorkerParser(startWorker: () => ParserWorker): (text: string) => Promise<Payload | undefined> {
  const waiting = new Map<number, Waiting>()
  let worker: ParserWorker | undefined
  let broken = false
  let nextId = 0

  function fallBack(): void {
    broken = true
    worker?.terminate()
    worker = undefined
    for (const entry of waiting.values()) entry.resolve(parseAssembled(entry.text))
    waiting.clear()
  }

  function ensureWorker(): ParserWorker | undefined {
    if (worker || broken) return worker
    try {
      worker = startWorker()
    } catch {
      broken = true
      return undefined
    }
    worker.onmessage = (event) => {
      waiting.get(event.data.id)?.resolve(event.data.payload)
      waiting.delete(event.data.id)
    }
    worker.onerror = fallBack
    return worker
  }

  return (text) => {
    const target = ensureWorker()
    if (!target) return Promise.resolve(parseAssembled(text))
    nextId += 1
    const id = nextId
    return new Promise((resolve) => {
      waiting.set(id, { text, resolve })
      target.postMessage({ id, text })
    })
  }
}
