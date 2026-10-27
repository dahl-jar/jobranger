import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SectionLabel } from '@/shared/ui/SectionLabel'

describe('SectionLabel', () => {
  it('should title a page section at level 2', () => {
    render(<SectionLabel>AI usage</SectionLabel>)

    expect(screen.getByRole('heading', { name: 'AI usage', level: 2 })).toBeInTheDocument()
  })

  it('should title a nested group at level 3', () => {
    render(<SectionLabel level={3}>Run times</SectionLabel>)

    expect(screen.getByRole('heading', { name: 'Run times', level: 3 })).toBeInTheDocument()
  })
})
