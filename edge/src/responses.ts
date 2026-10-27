const RETRY_AFTER_SECONDS = 60
const TOO_MANY_REQUESTS = 429
const NOT_FOUND = 404
const RATE_LIMITED = 'rate_limited'
const TOO_MANY_REQUESTS_TYPE = 'https://tools.ietf.org/html/rfc6585#section-4'
const PROBLEM_JSON = 'application/problem+json'

export function rateLimited(request: Request, siteOrigin: string): Response {
  const headers = new Headers({
    'Content-Type': PROBLEM_JSON,
    'Retry-After': String(RETRY_AFTER_SECONDS),
    'Cache-Control': 'no-store',
    Vary: 'Origin',
  })
  if (request.headers.get('Origin') === siteOrigin) {
    headers.set('Access-Control-Allow-Origin', siteOrigin)
    headers.set('Access-Control-Expose-Headers', 'Retry-After')
  }

  const problem = { type: TOO_MANY_REQUESTS_TYPE, title: RATE_LIMITED, status: TOO_MANY_REQUESTS, code: RATE_LIMITED }
  return new Response(JSON.stringify(problem), { status: TOO_MANY_REQUESTS, headers })
}

export function notFound(): Response {
  return new Response(null, { status: NOT_FOUND })
}
