import { describe, expect, it } from 'vitest'
import { FIT_LABELS } from '@/shared/ui/fit'

interface PayloadSchema {
  $defs: { Fit: { enum: string[] } }
}

const [payloadSchema] = Object.values(
  import.meta.glob<PayloadSchema>('@contracts/payload.schema.json', { eager: true, import: 'default' }),
)

describe('FIT_LABELS', () => {
  it('should label exactly the contract fits', () => {
    expect(Object.keys(FIT_LABELS).sort()).toEqual(payloadSchema?.$defs.Fit.enum.toSorted())
  })
})
