export const QUERY_KEYS = {
  status: ['status'],
  ads: ['ads'],
  applications: ['applications'],
  config: ['config'],
  profile: ['profile'],
  context: ['context'],
  guidance: ['guidance'],
  usage: ['usage'],
  models: ['models'],
  learning: ['learning'],
  run: ['run'],
} as const satisfies Record<string, readonly [string]>
