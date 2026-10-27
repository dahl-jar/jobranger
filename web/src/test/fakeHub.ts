import { createServer, type Server } from 'node:http'
import type { AddressInfo } from 'node:net'
import { type WebSocket, WebSocketServer } from 'ws'

const RECORD_SEPARATOR = '\u001e'
const INVOCATION = 1
const COMPLETION = 3
const BEARER = /^Bearer /

interface HubMessage {
  type: number
  target?: string
  invocationId?: string
  arguments?: unknown[]
}

export class FakeHub {
  readonly invocations: { target: string; arguments: unknown[] }[] = []
  readonly requestUrls: string[] = []
  readonly tokens: string[] = []
  clientClosed = false
  private readonly server: Server
  private readonly sockets = new WebSocketServer({ noServer: true })
  private socket: WebSocket | undefined

  private constructor(server: Server) {
    this.server = server
    server.on('upgrade', (request, socket, head) => {
      const url = new URL(request.url ?? '', 'http://127.0.0.1')
      this.requestUrls.push(url.pathname)
      this.tokens.push(url.searchParams.get('access_token') ?? request.headers.authorization?.replace(BEARER, '') ?? '')
      this.sockets.handleUpgrade(request, socket, head, (client) => this.accept(client))
    })
  }

  static async start(): Promise<FakeHub> {
    const hub = new FakeHub(createServer())
    await new Promise<void>((resolve) => hub.server.listen(0, '127.0.0.1', resolve))
    return hub
  }

  get url(): string {
    return `http://127.0.0.1:${(this.server.address() as AddressInfo).port}`
  }

  call(target: string, ...args: unknown[]): void {
    this.socket?.send(JSON.stringify({ type: INVOCATION, target, arguments: args }) + RECORD_SEPARATOR)
  }

  closeClient(): void {
    this.socket?.close()
  }

  async stop(): Promise<void> {
    this.socket?.terminate()
    this.sockets.close()
    await new Promise<void>((resolve) => this.server.close(() => resolve()))
  }

  private accept(client: WebSocket): void {
    this.socket = client
    client.on('close', () => {
      this.clientClosed = true
    })
    let shookHands = false
    client.on('message', (data) => {
      for (const frame of String(data).split(RECORD_SEPARATOR).filter((part) => part !== '')) {
        if (!shookHands) {
          shookHands = true
          client.send(`{}${RECORD_SEPARATOR}`)
          continue
        }
        this.onMessage(client, JSON.parse(frame) as HubMessage)
      }
    })
  }

  private onMessage(client: WebSocket, message: HubMessage): void {
    if (message.type !== INVOCATION) return
    this.invocations.push({ target: message.target ?? '', arguments: message.arguments ?? [] })
    if (message.invocationId !== undefined) {
      client.send(JSON.stringify({ type: COMPLETION, invocationId: message.invocationId, result: null }) + RECORD_SEPARATOR)
    }
  }
}
