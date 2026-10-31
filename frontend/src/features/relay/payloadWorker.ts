import { parseRequest, type ParseRequest } from '@/features/relay/payloadParser'

self.onmessage = (event: MessageEvent<ParseRequest>) => {
  self.postMessage(parseRequest(event.data))
}
