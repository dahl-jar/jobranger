const PREFERS_LIGHT_QUERY = '(prefers-color-scheme: light)'

const lists = new Set<FakeMediaQueryList>()
let prefersLight = false

class FakeMediaQueryList extends EventTarget {
  readonly media: string

  constructor(media: string) {
    super()
    this.media = media
  }

  get matches(): boolean {
    return this.media === PREFERS_LIGHT_QUERY && prefersLight
  }
}

export function installMatchMedia(): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: (media: string) => {
      const list = new FakeMediaQueryList(media)
      lists.add(list)
      return list
    },
  })
}

export function setPrefersLight(value: boolean): void {
  prefersLight = value
  lists.forEach((list) => list.dispatchEvent(new Event('change')))
}

export function resetMatchMedia(): void {
  prefersLight = false
  lists.clear()
}
