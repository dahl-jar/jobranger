# edge

Commands run from this folder.

Needs Node 24 and pnpm. Tests run in the Workers runtime through `@cloudflare/vitest-plugin`.

```bash
pnpm install --frozen-lockfile
pnpm typecheck
pnpm lint
pnpm test

# bundle the Worker without deploying it
pnpm build

# mutation run, local only
pnpm mutate
```

`wrangler.toml` holds the route and the rate limit bindings. `wrangler.test.ts` pins every limit, so a limit change updates the test in the same commit.

Under `wrangler dev`, Miniflare drops `CF-Connecting-IP` from the Worker's fetch to the origin, so a local relay sees every request from the dev server's address. On Cloudflare the edge sets the origin's `CF-Connecting-IP` from the `X-Real-IP` the Worker sends. A local test of per-address limits in the relay needs a proxy that copies the address into `CF-Connecting-IP`.
