# web

Commands run from this folder.

Needs Node 24 and pnpm. The font commands also need uv.

```bash
pnpm install --frozen-lockfile
pnpm fonts --check
pnpm typecheck
pnpm lint
pnpm test
pnpm build
pnpm mutate
```

Dev server with hot reload:

```bash
pnpm dev
```

Rebuild the subset fonts in `public/fonts` from the pinned Nerd Fonts release:

```bash
pnpm fonts
```
