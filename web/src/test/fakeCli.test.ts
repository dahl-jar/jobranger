import { describe, expect, it } from 'vitest'
import { decodeBase64Url, encodeBase64Url } from '@/features/relay/base64url'
import { answerOffer, beginHandshake } from '@/features/relay/session'
import { cliPairTag, FakeCli } from '@/test/fakeCli'
import { labelBytes16, SESSION_VECTOR as VECTOR, VECTOR_LABELS, vectorKeyPair } from '@/test/sessionVector'

describe('FakeCli', () => {
  it('should match the session vector', async () => {
    const cli = await FakeCli.create(await vectorKeyPair(VECTOR_LABELS.cliStatic, VECTOR.cli_static_public))
    const { commit, pending } = await beginHandshake({
      webStatic: await vectorKeyPair(VECTOR_LABELS.browserStatic, VECTOR.browser_static_public),
      agent: VECTOR.agent,
      secrets: {
        ephemeral: await vectorKeyPair(VECTOR_LABELS.browserEph, VECTOR.browser_eph_public),
        sid: await labelBytes16(VECTOR_LABELS.sid),
        nonceB: await labelBytes16(VECTOR_LABELS.nonce),
      },
    })
    await cli.offer(commit, await vectorKeyPair(VECTOR_LABELS.cliEph, VECTOR.cli_eph_public))
    const offer = { t: 'offer', cliStatic: decodeBase64Url(VECTOR.cli_static_public), cliEph: decodeBase64Url(VECTOR.cli_eph_public) } as const
    const { reveal } = await answerOffer(pending, offer, undefined)

    const mac = await cli.reveal(reveal, decodeBase64Url(pending.sid))

    expect({
      confirm_cli: encodeBase64Url(mac),
      confirm_browser: encodeBase64Url(await cli.mac('browser')),
      words: cli.words,
      frame_ciphertext: (await cli.seal(VECTOR.frame_plaintext)).ciphertext,
    }).toEqual({
      confirm_cli: VECTOR.confirm_cli,
      confirm_browser: VECTOR.confirm_browser,
      words: VECTOR.words,
      frame_ciphertext: VECTOR.frame_ciphertext,
    })
  })

  it('should match the vector pair_tag', async () => {
    expect(await cliPairTag(VECTOR.pair_code, decodeBase64Url(VECTOR.channel))).toBe(VECTOR.pair_tag)
  })
})
