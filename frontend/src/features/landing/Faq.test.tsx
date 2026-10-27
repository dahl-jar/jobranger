import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { Faq } from '@/features/landing/Faq'

const SEVEN_ROLES = ['backend', 'frontend', 'fullstack', 'mobile', 'data engineering', 'DevOps', 'ML and AI']

describe('Faq', () => {
  it('should open the first question', () => {
    render(<Faq />)

    expect(screen.getByRole('button', { name: 'Is it free?' })).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('Yes. You only pay your AI provider for what you use.')).toBeVisible()
  })

  it('should show the answer when a closed question is clicked', async () => {
    const user = userEvent.setup()
    render(<Faq />)
    const question = screen.getByRole('button', { name: 'Can I skip the website?' })

    await user.click(question)

    expect(question).toHaveAttribute('aria-expanded', 'true')
    expect(document.getElementById(question.getAttribute('aria-controls') ?? '')).toBeVisible()
  })

  it('should hide the answer when an open question is clicked', async () => {
    const user = userEvent.setup()
    render(<Faq />)
    const question = screen.getByRole('button', { name: 'Is it free?' })

    await user.click(question)

    expect(question).toHaveAttribute('aria-expanded', 'false')
    expect(screen.getByText('Yes. You only pay your AI provider for what you use.')).not.toBeVisible()
  })

  it('should list the seven roles under Which jobs', () => {
    render(<Faq />)
    const question = screen.getByRole('button', { name: 'Which jobs?' })
    const answer = document.getElementById(question.getAttribute('aria-controls') ?? '')

    for (const role of SEVEN_ROLES) expect(answer).toHaveTextContent(role)
  })
})
