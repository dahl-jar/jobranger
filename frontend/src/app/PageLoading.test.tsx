import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PageLoading } from '@/app/PageLoading'

describe('PageLoading', () => {
  it('should say the page is loading', () => {
    render(<PageLoading />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading...')
  })
})
