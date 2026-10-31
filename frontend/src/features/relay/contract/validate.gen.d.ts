import type { ErrorObject } from 'ajv'
import type { Payload } from './payload.gen'
import type { Envelope } from './envelope.gen'

export interface StandaloneValidator<T> {
  (data: unknown): data is T
  errors?: ErrorObject[] | null
}

export declare const validatePayload: StandaloneValidator<Payload>
export declare const validateEnvelope: StandaloneValidator<Envelope>
