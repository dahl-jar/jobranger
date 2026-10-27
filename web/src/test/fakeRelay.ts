import { decodeBase64Url, encodeBase64Url } from '@/features/relay/base64url'
import type { Envelope } from '@/features/relay/contract/envelope.gen'
import type { ConnectRelay, RelayEvents } from '@/features/relay/transport'
import { cliPairTag, FakeCli } from '@/test/fakeCli'

export const TEST_CHANNEL = 'QzATLaCZ1ciBwANAgYReA3Np4WJ48r7ZPzlZsVwdxSQ'

const REFILL_PERIOD_MS = 1000
const RELAY_MESSAGES_BURST = 40
const RELAY_MESSAGES_PER_SECOND = 20
const RELAY_BYTES_BURST = 524_288
const RELAY_BYTES_PER_SECOND = 262_144

type BrowserStep = Record<string, string>

export function readHello(ciphertext: string): BrowserStep {
  return JSON.parse(new TextDecoder().decode(decodeBase64Url(ciphertext))) as BrowserStep
}

function readStep(envelope: Envelope): BrowserStep {
  return readHello(envelope.ciphertext)
}

export function helloText(step: Record<string, unknown>): string {
  return encodeBase64Url(new TextEncoder().encode(JSON.stringify(step)))
}

export class VirtualClock {
  ms = 0

  now = (): number => this.ms

  wait = async (ms: number): Promise<void> => {
    this.ms += ms
  }
}

class RelayBucket {
  readonly #burst: number
  readonly #refill: number
  #tokens: number
  #lastRefill: number

  constructor(burst: number, refill: number, now: number) {
    this.#burst = burst
    this.#refill = refill
    this.#tokens = burst
    this.#lastRefill = now
  }

  take(count: number, now: number): boolean {
    const periods = Math.floor((now - this.#lastRefill) / REFILL_PERIOD_MS)
    this.#tokens = Math.min(this.#burst, this.#tokens + periods * this.#refill)
    this.#lastRefill += periods * REFILL_PERIOD_MS
    if (count > this.#tokens) return false
    this.#tokens -= count
    return true
  }
}

export class FakeRelay {
  readonly sent: Envelope[] = []
  readonly clock = new VirtualClock()
  limitRate = false
  readonly handles: string[] = []
  events: RelayEvents | undefined
  stopped = false
  failSends = false
  sendGate: Promise<void> | undefined
  failNextSend = false
  sendCalls = 0
  onlineAtConnect = false
  peer: CliPeer | undefined

  connect: ConnectRelay = async (handle, events) => {
    this.handles.push(handle)
    this.events = events
    const messages = new RelayBucket(RELAY_MESSAGES_BURST, RELAY_MESSAGES_PER_SECOND, this.clock.now())
    const bytes = new RelayBucket(RELAY_BYTES_BURST, RELAY_BYTES_PER_SECOND, this.clock.now())
    if (this.onlineAtConnect) events.onPresence({ cli_online: true })
    return {
      send: async (envelope) => {
        this.sendCalls += 1
        await this.sendGate
        if (this.failNextSend) {
          this.failNextSend = false
          throw new Error('relay refused the send')
        }
        if (this.failSends) throw new Error('relay refused the send')
        const now = this.clock.now()
        if (this.limitRate && !(messages.take(1, now) && bytes.take(envelope.ciphertext.length, now))) throw new Error('rate_limited')
        this.sent.push(envelope)
        await this.peer?.receive(envelope)
      },
      stop: async () => {
        this.stopped = true
      },
    }
  }

  presence(cliOnline: boolean): void {
    this.events?.onPresence({ cli_online: cliOnline })
  }

  deliver(envelope: Envelope | string): void {
    this.events?.onEnvelope(typeof envelope === 'string' ? envelope : JSON.stringify(envelope))
  }

  drop(): void {
    this.events?.onClose()
  }

  steps(): string[] {
    return this.sent.map((envelope) => (envelope.kind === 'hello' ? readStep(envelope).t ?? '' : 'data'))
  }

  sids(): string[] {
    return [...new Set(this.sent.map((envelope) => envelope.sid))]
  }
}

export interface PeerOptions {
  pairCode?: string
  confirmMac?: (mac: Uint8Array) => Uint8Array
  replyToCommit?: boolean
  replyToReveal?: boolean
}

export class CliPeer {
  readonly cli: FakeCli
  readonly relay: FakeRelay
  readonly options: PeerOptions
  sid = ''
  browserConfirmed = false
  readonly received: string[] = []
  readonly commits: BrowserStep[] = []

  constructor(cli: FakeCli, relay: FakeRelay, options: PeerOptions = {}) {
    this.cli = cli
    this.relay = relay
    this.options = options
    relay.peer = this
  }

  static async attach(relay: FakeRelay, options: PeerOptions = {}, staticPair?: CryptoKeyPair): Promise<CliPeer> {
    return new CliPeer(await FakeCli.create(staticPair), relay, options)
  }

  hello(step: Record<string, unknown>, sid = this.sid): Envelope {
    return { v: 1, channel: TEST_CHANNEL, sid, seq: 0, kind: 'hello', ciphertext: helloText(step) }
  }

  sendHello(step: Record<string, unknown>, sid = this.sid): void {
    this.relay.deliver(this.hello(step, sid))
  }

  async sendData(plaintext: string | Uint8Array, seq?: number): Promise<void> {
    const frame = await this.cli.seal(plaintext, seq)
    this.relay.deliver({ v: 1, channel: TEST_CHANNEL, sid: this.sid, seq: frame.seq, kind: 'data', ciphertext: frame.ciphertext })
  }

  async receive(envelope: Envelope): Promise<void> {
    if (envelope.kind === 'data') {
      this.received.push(await this.cli.open(envelope.seq, envelope.ciphertext))
      return
    }
    const step = readStep(envelope)
    if (step.t === 'commit') this.commits.push(step)
    if (step.t === 'commit' && this.options.replyToCommit !== false && (await this.acceptsTag(step))) await this.onCommit(envelope.sid, step)
    if (step.t === 'reveal' && this.options.replyToReveal !== false) await this.onReveal(step)
    if (step.t === 'confirm') this.browserConfirmed = await this.verifyBrowser(step)
  }

  private async acceptsTag(step: BrowserStep): Promise<boolean> {
    if (this.options.pairCode === undefined) return true
    return step.pair_tag === (await cliPairTag(this.options.pairCode, decodeBase64Url(TEST_CHANNEL)))
  }

  private async onCommit(sid: string, step: BrowserStep): Promise<void> {
    this.sid = sid
    this.browserConfirmed = false
    const offer = await this.cli.offer({ browserStatic: decodeBase64Url(step.browser_static ?? ''), commit: decodeBase64Url(step.commit ?? '') })
    this.sendHello({ t: 'offer', cli_static: encodeBase64Url(offer.cliStatic), cli_eph: encodeBase64Url(offer.cliEph) })
  }

  private async onReveal(step: BrowserStep): Promise<void> {
    const reveal = { browserEph: decodeBase64Url(step.browser_eph ?? ''), nonceB: decodeBase64Url(step.nonce_b ?? ''), agent: step.agent ?? '' }
    const mac = await this.cli.reveal(reveal, decodeBase64Url(this.sid))
    this.sendHello({ t: 'confirm', mac: encodeBase64Url(this.options.confirmMac?.(mac) ?? mac) })
  }

  private async verifyBrowser(step: BrowserStep): Promise<boolean> {
    return encodeBase64Url(await this.cli.mac('browser')) === step.mac
  }
}
