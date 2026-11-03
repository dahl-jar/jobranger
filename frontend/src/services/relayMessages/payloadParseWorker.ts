import { parseRequest, type ParseRequest } from '@/services/relayMessages/workerPayloadParser'

self.onmessage = (event: MessageEvent<ParseRequest>) => {
  self.postMessage(parseRequest(event.data))
}
