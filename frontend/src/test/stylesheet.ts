import { afterEach, beforeEach } from 'vitest'

interface FileSystem {
  readFileSync(path: string, encoding: 'utf8'): string
}

const FS_MODULE = 'node:fs'
const CLASS_SELECTOR = /\.([a-zA-Z][\w-]*)/g

const fs: FileSystem = await import(FS_MODULE)

export function attachStylesheet(path: string, classNames: Readonly<Record<string, string | undefined>>): void {
  const sheet = document.createElement('style')
  sheet.textContent = fs.readFileSync(path, 'utf8').replace(CLASS_SELECTOR, (_, name: string) => `.${classNames[name] ?? name}`)

  beforeEach(() => {
    document.head.append(sheet)
  })

  afterEach(() => {
    sheet.remove()
  })
}
