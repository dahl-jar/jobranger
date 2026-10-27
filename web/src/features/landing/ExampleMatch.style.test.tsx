import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ExampleMatch } from '@/features/landing/ExampleMatch'
import styles from '@/features/landing/LandingPage.module.css'
import { renderNorwegian } from '@/test/render'
import { attachStylesheet } from '@/test/stylesheet'

const DATE_COLUMN_WIDTH = '92px'

attachStylesheet('src/features/landing/LandingPage.module.css', styles)

describe('ExampleMatch styles', () => {
  it('should let the fit label wrap inside the fixed date column', async () => {
    renderNorwegian(<ExampleMatch />)

    const label = await screen.findByText('Passer delvis')

    expect(label.textContent).toContain(' ')
    expect(getComputedStyle(label.closest(`.${styles.due}`) ?? document.body).width).toBe(DATE_COLUMN_WIDTH)
  })
})
