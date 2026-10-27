import { type HubMessage, MessageType, NullLogger } from '@microsoft/signalr'
import { describe, expect, it } from 'vitest'
import { RawReceiveProtocol } from '@/features/relay/rawReceiveProtocol'

const RS = '\u001e'

function parse(...frames: string[]): HubMessage[] {
  return new RawReceiveProtocol().parseMessages(frames.map((frame) => frame + RS).join(''), NullLogger.instance)
}

function receiveArguments(frame: string): unknown {
  const [message] = parse(frame)
  return message?.type === MessageType.Invocation ? message.arguments : undefined
}

describe('RawReceiveProtocol', () => {
  it.each([
    ['duplicate keys', '{"seq":1,"seq":2}'],
    ['a long integer', '{"seq":12345678901234567890}'],
    ['braces and quotes inside strings', '{"ciphertext":"a}]\\"[{b"}'],
    ['nested objects', '{"a":{"b":[1,{"c":2}]},"d":null}'],
  ])('should hand over the Receive argument verbatim with %s', (_, envelope) => {
    const args = receiveArguments(`{"type":1,"target":"Receive","arguments":[${envelope}]}`)

    expect(args).toEqual([envelope])
  })

  it('should find the arguments after other members and whitespace', () => {
    const args = receiveArguments('{ "target" : "Receive" , "headers":{"a":"]"}, "note":"a, b", "count":123, "type":1, "arguments" : [ {"seq":1} , 2 ] }')

    expect(args).toEqual(['{"seq":1}'])
  })

  it('should hand over an empty text when Receive has no argument', () => {
    const args = receiveArguments('{"type":1,"target":"Receive","arguments":[]}')

    expect(args).toEqual([''])
  })

  it('should hand over an empty text when Receive has no arguments member', () => {
    const args = receiveArguments('{"type":1,"target":"Receive"}')

    expect(args).toEqual([''])
  })

  it('should keep the parsed arguments of other targets', () => {
    const args = receiveArguments('{"type":1,"target":"Presence","arguments":[{"cli_online":true,"browsers":1}]}')

    expect(args).toEqual([{ cli_online: true, browsers: 1 }])
  })

  it('should leave a reply frame that names Receive as parsed', () => {
    const [message] = parse('{"type":3,"invocationId":"1","target":"Receive","result":null}')

    expect(message).toEqual({ type: MessageType.Completion, invocationId: '1', target: 'Receive', result: null })
  })

  it('should read every frame in one input', () => {
    const messages = parse('{"type":6}', '{"type":1,"target":"Receive","arguments":["x"]}')

    expect(messages).toEqual([
      { type: MessageType.Ping },
      { type: MessageType.Invocation, target: 'Receive', arguments: ['"x"'] },
    ])
  })

  it('should refuse binary input', () => {
    expect(() => new RawReceiveProtocol().parseMessages(new ArrayBuffer(1), NullLogger.instance)).toThrow('the JSON hub protocol reads text only')
  })

  it('should write messages as the JSON protocol does', () => {
    const text = new RawReceiveProtocol().writeMessage({ type: MessageType.Ping })

    expect(text).toBe(`{"type":6}${RS}`)
  })
})
