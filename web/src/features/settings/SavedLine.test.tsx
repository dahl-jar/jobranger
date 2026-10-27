import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { payloadFixtures } from '@/features/relay/contract/fixtures.gen'
import { SavedLine } from '@/features/settings/SavedLine'

describe('SavedLine', () => {
  it('should add the recheck line after a saved reply that carries recheck', () => {
    render(<SavedLine result={{ kind: 'saved', recheck: payloadFixtures.saved.recheck }} />)

    expect(screen.getByRole('status')).toHaveTextContent('Saved')
    expect(screen.getByRole('status')).toHaveTextContent('Rechecks 37 ads on the next run, up to about $0.12')
  })

  it('should say only Saved when the reply has no recheck', () => {
    render(<SavedLine result={{ kind: 'saved', recheck: undefined }} />)

    expect(screen.getByRole('status')).toHaveTextContent(/^Saved$/)
  })

  it("should say Not saved with the machine's message", () => {
    render(<SavedLine result={{ kind: 'failed', message: payloadFixtures.error.message }} />)

    expect(screen.getByRole('status')).toHaveTextContent('Not saved: Run times must be at least 2 hours apart')
  })

  it('should keep an empty status line before anything is saved', () => {
    render(<SavedLine result={undefined} />)

    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })
})
