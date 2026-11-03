import { type HubMessage, type IHubProtocol, type ILogger, JsonHubProtocol, MessageType } from '@microsoft/signalr'

const RECORD_SEPARATOR = '\u001e'
const RECEIVE_TARGET = 'Receive'
const ARGUMENTS_KEY = 'arguments'
const WHITESPACE = /\s/
const SCALAR_END = /[\s,\]}]/
const OPENERS = new Set(['{', '['])
const CLOSERS = new Set(['}', ']'])

function skipWhitespace(text: string, start: number): number {
  let index = start
  while (WHITESPACE.test(text.charAt(index))) index += 1
  return index
}

function stringEnd(text: string, start: number): number {
  let index = start + 1
  while (index < text.length && text[index] !== '"') index += text[index] === '\\' ? 2 : 1
  return index + 1
}

function containerEnd(text: string, start: number): number {
  let depth = 0
  let index = start
  do {
    const char = text.charAt(index)
    if (char === '"') {
      index = stringEnd(text, index)
      continue
    }
    if (OPENERS.has(char)) depth += 1
    if (CLOSERS.has(char)) depth -= 1
    index += 1
  } while (depth > 0 && index < text.length)
  return index
}

function valueEnd(text: string, start: number): number {
  const char = text.charAt(start)
  if (char === '"') return stringEnd(text, start)
  if (OPENERS.has(char)) return containerEnd(text, start)
  let index = start
  while (index < text.length && !SCALAR_END.test(text.charAt(index))) index += 1
  return index
}

function memberValueStart(text: string, key: string): number | undefined {
  let index = skipWhitespace(text, skipWhitespace(text, 0) + 1)
  while (text.charAt(index) === '"') {
    const nameEnd = stringEnd(text, index)
    const name: unknown = JSON.parse(text.slice(index, nameEnd))
    const valueStart = skipWhitespace(text, skipWhitespace(text, nameEnd) + 1)
    if (name === key) return valueStart
    index = skipWhitespace(text, valueEnd(text, valueStart))
    index = skipWhitespace(text, index + 1)
  }
  return undefined
}

function firstArgumentText(frame: string): string {
  const arrayStart = memberValueStart(frame, ARGUMENTS_KEY)
  if (arrayStart === undefined) return ''
  const start = skipWhitespace(frame, arrayStart + 1)
  return frame.slice(start, valueEnd(frame, start))
}

export class RawReceiveProtocol implements IHubProtocol {
  readonly #json = new JsonHubProtocol()
  readonly name = this.#json.name
  readonly version = this.#json.version
  readonly transferFormat = this.#json.transferFormat

  parseMessages(input: string | ArrayBuffer, logger: ILogger): HubMessage[] {
    if (typeof input !== 'string') throw new Error('the JSON hub protocol reads text only')
    return input
      .split(RECORD_SEPARATOR)
      .filter((frame) => frame !== '')
      .flatMap((frame) =>
        this.#json.parseMessages(frame + RECORD_SEPARATOR, logger).map((message) =>
          message.type === MessageType.Invocation && message.target === RECEIVE_TARGET
            ? { ...message, arguments: [firstArgumentText(frame)] }
            : message,
        ),
      )
  }

  writeMessage(message: HubMessage): string {
    return this.#json.writeMessage(message)
  }
}
