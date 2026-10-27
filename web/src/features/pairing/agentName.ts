const BROWSERS: readonly [RegExp, string][] = [
  [/Edg(A|iOS)?\//, 'Edge'],
  [/Firefox\/|FxiOS\//, 'Firefox'],
  [/Chrome\/|CriOS\//, 'Chrome'],
  [/Safari\//, 'Safari'],
]

const SYSTEMS: readonly [RegExp, string][] = [
  [/iPhone|iPad|iPod/, 'iOS'],
  [/Android/, 'Android'],
  [/Windows/, 'Windows'],
  [/Macintosh|Mac OS X/, 'macOS'],
  [/Linux|X11/, 'Linux'],
]

const UNKNOWN_AGENT = 'a browser'

function firstMatch(userAgent: string, table: readonly [RegExp, string][]): string | undefined {
  return table.find(([pattern]) => pattern.test(userAgent))?.[1]
}

export function agentName(userAgent: string): string {
  const browser = firstMatch(userAgent, BROWSERS)
  const system = firstMatch(userAgent, SYSTEMS)
  if (browser === undefined) return UNKNOWN_AGENT
  return system === undefined ? browser : `${browser} on ${system}`
}
