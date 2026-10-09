# Contributing

Each component builds and tests on its own. Run the commands from the component folder.

## cli

Needs Python 3.12+ and uv.

```bash
uv sync --locked
uv run ruff check
uv run ruff format --check
uv run mypy
uv run pytest
uv run python scripts/mutate.py
```

## relay

Needs the .NET 10 SDK.

```bash
dotnet restore Jobranger.Relay.slnx
dotnet format Jobranger.Relay.slnx --verify-no-changes --no-restore
dotnet build Jobranger.Relay.slnx -c Release --no-restore
dotnet test --solution Jobranger.Relay.slnx -c Release --no-build
dotnet tool restore
dotnet stryker
```

## web

Needs Node 24 and pnpm.

```bash
pnpm install --frozen-lockfile
pnpm typecheck
pnpm lint
pnpm test
pnpm build
pnpm mutate
```

## contracts

Run from the repo root. Generated code must match the schemas.

```bash
bash contracts/generate.sh
git diff --exit-code
```

## Mutation runs

`scripts/mutate.py`, `dotnet stryker` and `pnpm mutate` run locally only. `dotnet stryker` needs at least one relay test.

## Commits

One change per commit, with a short subject line and no body.
