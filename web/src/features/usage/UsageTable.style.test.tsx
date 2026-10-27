import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import styles from '@/features/usage/usage.module.css'
import { TODAY, usage } from '@/features/usage/usage.samples'
import { UsageTable } from '@/features/usage/UsageTable'

interface FileSystem {
  readFileSync(path: string, encoding: 'utf8'): string
}

const FS_MODULE = 'node:fs'
const STYLESHEET_PATH = 'src/features/usage/usage.module.css'
const CLASS_SELECTOR = /\.([a-zA-Z][\w-]*)/g
const COLUMN_GAP = '12px'

const fs: FileSystem = await import(FS_MODULE)
const sheet = document.createElement('style')
sheet.textContent = fs
  .readFileSync(STYLESHEET_PATH, 'utf8')
  .replace(CLASS_SELECTOR, (_, name: string) => `.${styles[name] ?? name}`)

beforeEach(() => {
  document.head.append(sheet)
})

afterEach(() => {
  sheet.remove()
})

describe('UsageTable styles', () => {
  it('should keep the column gap between Tokens and Cost', () => {
    render(<UsageTable usage={usage} today={TODAY} />)

    expect(getComputedStyle(screen.getByRole('cell', { name: '210k' })).paddingRight).toBe(COLUMN_GAP)
  })
})
