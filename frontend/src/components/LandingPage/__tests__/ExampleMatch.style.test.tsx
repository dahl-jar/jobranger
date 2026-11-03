import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ExampleMatch } from '@/components/LandingPage/ExampleMatch'
import styles from '@/components/LandingPage/LandingPage.module.css'
import { renderNorwegian } from '@/__tests__/renderWithProviders'
import { attachStylesheet } from '@/__tests__/attachStylesheet'

const DATE_COLUMN_WIDTH = '92px'

attachStylesheet('src/components/LandingPage/LandingPage.module.css', styles)

describe('ExampleMatch styles', () => {
  it('should let the fit label wrap inside the fixed date column', async () => {
    renderNorwegian(<ExampleMatch />)

    const label = await screen.findByText('Passer delvis')

    expect(label.textContent).toContain(' ')
    expect(getComputedStyle(label.closest(`.${styles.due}`) ?? document.body).width).toBe(DATE_COLUMN_WIDTH)
  })
})
