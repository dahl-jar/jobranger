import { describe, expect, it } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { parseRequest, createWorkerParser, type ParseReply, type ParseRequest, type ParserWorker } from '@/features/relay/payloadParser'

const STATUS = payloadFixtures.status
const SAVED = payloadFixtures.saved

class FakeWorker implements ParserWorker {
  readonly requests: ParseRequest[] = []
  terminated = false
  onmessage: ((event: MessageEvent<ParseReply>) => void) | null = null
  onerror: ((event: ErrorEvent) => void) | null = null

  postMessage(request: ParseRequest): void {
    this.requests.push(request)
  }

  terminate(): void {
    this.terminated = true
  }

  reply(index: number): void {
    const request = this.requests[index]
    if (request) this.onmessage?.(new MessageEvent('message', { data: parseRequest(request) }))
  }

  crash(): void {
    this.onerror?.(new Event('error') as ErrorEvent)
  }
}

describe('createWorkerParser', () => {
  it('should parse the text in the worker', async () => {
    const worker = new FakeWorker()
    const parse = createWorkerParser(() => worker)

    const parsing = parse(JSON.stringify(STATUS))
    worker.reply(0)

    expect([await parsing, worker.requests.length]).toEqual([STATUS, 1])
  })

  it('should match each reply to its own request', async () => {
    const worker = new FakeWorker()
    const parse = createWorkerParser(() => worker)

    const first = parse(JSON.stringify(STATUS))
    const second = parse(JSON.stringify(SAVED))
    worker.reply(1)
    worker.reply(0)

    expect(await Promise.all([first, second])).toEqual([STATUS, SAVED])
  })

  it('should parse in place when the worker fails', async () => {
    const worker = new FakeWorker()
    const parse = createWorkerParser(() => worker)

    const parsing = parse(JSON.stringify(STATUS))
    worker.crash()

    expect([await parsing, await parse(JSON.stringify(SAVED)), worker.requests.length, worker.terminated]).toEqual([STATUS, SAVED, 1, true])
  })

  it('should parse in place without retrying a worker that cannot start', async () => {
    let attempts = 0
    const parse = createWorkerParser(() => {
      attempts += 1
      throw new Error('worker blocked')
    })

    const parsed = [await parse(JSON.stringify(STATUS)), await parse(JSON.stringify(SAVED))]

    expect([parsed, attempts]).toEqual([[STATUS, SAVED], 1])
  })

  it('should start the worker only for the first text', async () => {
    let started = 0
    const worker = new FakeWorker()
    const parse = createWorkerParser(() => {
      started += 1
      return worker
    })

    const parsing = [parse(JSON.stringify(STATUS)), parse(JSON.stringify(SAVED))]
    worker.reply(0)
    worker.reply(1)
    await Promise.all(parsing)

    expect(started).toBe(1)
  })
})

describe('parseRequest', () => {
  it('should answer with no payload for text that breaks the contract', () => {
    expect(parseRequest({ id: 3, text: '{"type":"status"}' })).toEqual({ id: 3, payload: undefined })
  })
})
