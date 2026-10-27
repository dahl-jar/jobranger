import { afterEach, describe, expect, it, vi } from 'vitest'
import { decodeBase64Url, encodeBase64Url } from '@/features/relay/base64url'
import { CIPHERTEXT_B64_MAX_CHARS } from '@/features/relay/contract/limits'
import { HelloError } from '@/features/relay/hello'
import { answerOffer, beginHandshake, createHandshakeSecrets, FrameError, finishHandshake, HandshakeError, pairTag, type Session } from '@/features/relay/session'
import { FakeCli } from '@/test/fakeCli'
import { SESSION_VECTOR as VECTOR, vectorStart } from '@/test/sessionVector'

const OFFER = { t: 'offer', cliStatic: decodeBase64Url(VECTOR.cli_static_public), cliEph: decodeBase64Url(VECTOR.cli_eph_public) } as const
const PLAINTEXT = new TextEncoder().encode('{"type":"unpair","v":1,"id":"00000000-0000-4000-8000-000000000001"}')

afterEach(() => {
  vi.restoreAllMocks()
})

async function vectorFinish() {
  const { pending } = await vectorStart()
  const { offered } = await answerOffer(pending, OFFER, undefined)
  return finishHandshake(offered, { t: 'confirm', mac: decodeBase64Url(VECTOR.confirm_cli) })
}

async function liveSession(): Promise<{ cli: FakeCli; session: Session }> {
  const cli = await FakeCli.create()
  const webStatic = await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits'])
  const { commit, pending } = await beginHandshake({ webStatic, agent: 'Firefox on macOS' })
  const offer = await cli.offer(commit)
  const { reveal, offered } = await answerOffer(pending, { t: 'offer', ...offer }, undefined)
  const mac = await cli.reveal(reveal, decodeBase64Url(pending.sid))
  const { session } = await finishHandshake(offered, { t: 'confirm', mac })
  return { cli, session }
}

describe('session vector', () => {
  it('should build the vector commit and sid', async () => {
    const { commit, pending } = await vectorStart()

    expect([encodeBase64Url(commit.browserStatic), encodeBase64Url(commit.commit), pending.sid]).toEqual([
      VECTOR.browser_static_public,
      VECTOR.commit,
      VECTOR.sid,
    ])
  })

  it('should reveal the vector ephemeral key, nonce and agent', async () => {
    const { pending } = await vectorStart()

    const { reveal } = await answerOffer(pending, OFFER, undefined)

    expect([encodeBase64Url(reveal.browserEph), encodeBase64Url(reveal.nonceB), reveal.agent]).toEqual([
      VECTOR.browser_eph_public,
      VECTOR.nonce_b,
      VECTOR.agent,
    ])
  })

  it('should answer the vector CLI confirm with the vector browser confirm and words', async () => {
    const { confirm, words } = await vectorFinish()

    expect([encodeBase64Url(confirm.mac), words]).toEqual([VECTOR.confirm_browser, VECTOR.words])
  })

  it('should decrypt the vector frame', async () => {
    const { session } = await vectorFinish()

    const plaintext = await session.open(VECTOR.frame_seq, VECTOR.frame_ciphertext)

    expect(new TextDecoder().decode(plaintext)).toBe(VECTOR.frame_plaintext)
  })
})

describe('pairTag', () => {
  it('should match the vector pair_tag', async () => {
    expect(encodeBase64Url(await pairTag(VECTOR.pair_code, VECTOR.channel))).toBe(VECTOR.pair_tag)
  })
})

describe('beginHandshake', () => {
  it('should put the pair tag into the commit while pairing', async () => {
    const webStatic = await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits'])
    const tag = await pairTag(VECTOR.pair_code, VECTOR.channel)

    const { commit } = await beginHandshake({ webStatic, agent: 'a', pairTag: tag })

    expect(commit.pairTag).toEqual(tag)
  })

  it('should leave the pair tag out of a paired commit', async () => {
    const webStatic = await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits'])

    const { commit } = await beginHandshake({ webStatic, agent: 'a' })

    expect('pairTag' in commit).toBe(false)
  })

  it('should pick a new sid for every handshake', async () => {
    const webStatic = await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits'])

    const sids = await Promise.all([1, 2].map(async () => (await beginHandshake({ webStatic, agent: 'a' })).pending.sid))

    expect(new Set(sids).size).toBe(2)
  })

  it('should make the ephemeral private key non-extractable', async () => {
    const { ephemeral } = await createHandshakeSecrets()

    expect(ephemeral.privateKey.extractable).toBe(false)
  })

  it('should refuse its own agent over 256 bytes', async () => {
    const webStatic = await crypto.subtle.generateKey({ name: 'X25519' }, false, ['deriveBits'])

    await expect(beginHandshake({ webStatic, agent: 'a'.repeat(257) })).rejects.toThrow(HelloError)
  })
})

describe('answerOffer', () => {
  it('should accept an offer whose CLI key equals the stored one', async () => {
    const { pending } = await vectorStart()

    const { reveal } = await answerOffer(pending, OFFER, decodeBase64Url(VECTOR.cli_static_public))

    expect(encodeBase64Url(reveal.browserEph)).toBe(VECTOR.browser_eph_public)
  })

  it.each([
    { name: 'a different key', stored: () => decodeBase64Url(VECTOR.cli_eph_public) },
    { name: 'one byte off', stored: () => decodeBase64Url(VECTOR.cli_static_public).map((byte, index) => (index === 31 ? byte ^ 1 : byte)) },
  ])('should throw HandshakeError cli_key when the stored CLI key is $name', async ({ stored }) => {
    const { pending } = await vectorStart()

    await expect(answerOffer(pending, OFFER, stored())).rejects.toThrow(new HandshakeError('cli_key'))
  })

  it('should make the session encryption and MAC keys non-extractable', async () => {
    const { pending } = await vectorStart()

    const { offered } = await answerOffer(pending, OFFER, undefined)

    expect([offered.sendKey, offered.receiveKey, offered.confirmKey].map((key) => key.extractable)).toEqual([false, false, false])
  })

  it('should throw HandshakeError zero when an engine returns an all-zero secret', async () => {
    const { pending } = await vectorStart()
    vi.spyOn(crypto.subtle, 'deriveBits').mockResolvedValue(new ArrayBuffer(32))

    const offering = answerOffer(pending, OFFER, undefined)

    await expect(offering).rejects.toThrow(new HandshakeError('zero'))
  })

  it('should throw HandshakeError zero when an offer key gives an all-zero secret', async () => {
    const { pending } = await vectorStart()

    await expect(answerOffer(pending, { ...OFFER, cliEph: new Uint8Array(32) }, undefined)).rejects.toThrow(new HandshakeError('zero'))
  })
})

describe('finishHandshake', () => {
  it('should throw HandshakeError mac when the CLI MAC differs', async () => {
    const { pending } = await vectorStart()
    const { offered } = await answerOffer(pending, OFFER, undefined)

    await expect(finishHandshake(offered, { t: 'confirm', mac: decodeBase64Url(VECTOR.confirm_browser) })).rejects.toThrow(
      new HandshakeError('mac'),
    )
  })

  it('should keep the offered CLI key on the session', async () => {
    const { session } = await vectorFinish()

    expect(encodeBase64Url(session.cliStatic)).toBe(VECTOR.cli_static_public)
  })
})

describe('Session', () => {
  it('should seal frames the CLI opens with seq 1 then 2', async () => {
    const { cli, session } = await liveSession()

    const first = await session.seal(PLAINTEXT)
    const second = await session.seal(PLAINTEXT)

    expect([first.seq, second.seq, await cli.open(second.seq, second.ciphertext)]).toEqual([1, 2, new TextDecoder().decode(PLAINTEXT)])
  })

  it('should open frames numbered 1 then 2', async () => {
    const { cli, session } = await liveSession()
    const first = await cli.seal('one')
    const second = await cli.seal('two')

    await session.open(first.seq, first.ciphertext)

    expect(new TextDecoder().decode(await session.open(second.seq, second.ciphertext))).toBe('two')
  })

  it.each([
    { rule: 'a replayed seq', next: 1 },
    { rule: 'a skipped seq', next: 3 },
  ])('should throw FrameError seq for $rule', async ({ next }) => {
    const { cli, session } = await liveSession()
    const first = await cli.seal('one')
    await session.open(first.seq, first.ciphertext)
    const second = await cli.seal('two', next)

    await expect(session.open(second.seq, second.ciphertext)).rejects.toThrow(new FrameError('seq'))
  })

  it('should throw FrameError tag when the sid in the AAD differs', async () => {
    const { cli, session } = await liveSession()
    const frame = await cli.seal('one', 1, new Uint8Array(16))

    await expect(session.open(frame.seq, frame.ciphertext)).rejects.toThrow(new FrameError('tag'))
  })

  it('should throw FrameError tag for a truncated tag', async () => {
    const { cli, session } = await liveSession()
    const frame = await cli.seal('one')

    await expect(session.open(frame.seq, encodeBase64Url(decodeBase64Url(frame.ciphertext).slice(0, -1)))).rejects.toThrow(
      new FrameError('tag'),
    )
  })

  it('should refuse a valid frame after a rejected one', async () => {
    const { cli, session } = await liveSession()
    const frame = await cli.seal('one')
    await expect(session.open(2, frame.ciphertext)).rejects.toThrow(FrameError)

    await expect(session.open(frame.seq, frame.ciphertext)).rejects.toThrow(new FrameError('closed'))
  })

  it('should refuse to seal after a rejected frame', async () => {
    const { session } = await liveSession()
    await expect(session.open(1, encodeBase64Url(new Uint8Array(16)))).rejects.toThrow(FrameError)

    await expect(session.seal(PLAINTEXT)).rejects.toThrow(new FrameError('closed'))
  })

  it.each([
    { rule: 'over CIPHERTEXT_B64_MAX_CHARS', ciphertext: 'A'.repeat(CIPHERTEXT_B64_MAX_CHARS + 2) },
    { rule: 'under 16 bytes', ciphertext: encodeBase64Url(new Uint8Array(15)) },
    { rule: 'padded base64url', ciphertext: `${encodeBase64Url(new Uint8Array(17))}=` },
  ])('should throw FrameError size for a ciphertext $rule', async ({ ciphertext }) => {
    const { session } = await liveSession()

    await expect(session.open(1, ciphertext)).rejects.toThrow(new FrameError('size'))
  })

  it('should accept a ciphertext of exactly CIPHERTEXT_B64_MAX_CHARS as far as the tag check', async () => {
    const { session } = await liveSession()

    await expect(session.open(1, 'A'.repeat(CIPHERTEXT_B64_MAX_CHARS))).rejects.toThrow(new FrameError('tag'))
  })

  it('should refuse frames after close', async () => {
    const { cli, session } = await liveSession()
    const frame = await cli.seal('one')

    session.close()

    await expect(session.open(frame.seq, frame.ciphertext)).rejects.toThrow(new FrameError('closed'))
  })

  it('should accept a ciphertext of exactly 16 bytes as far as the tag check', async () => {
    const { session } = await liveSession()

    await expect(session.open(1, encodeBase64Url(new Uint8Array(16)))).rejects.toThrow(new FrameError('tag'))
  })
})
