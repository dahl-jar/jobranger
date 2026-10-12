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

```bash
# dev server with hot reload
pnpm dev

# rebuild the subset fonts in public/fonts from the pinned Nerd Fonts release
pnpm fonts
```

## contracts

Run from the repo root. Generated code must match the schemas.

```bash
bash contracts/generate.sh
git diff --exit-code
```

The fixture check runs with the CLI tests. Every golden in `contracts/fixtures/envelope` and `contracts/fixtures/payload` must pass its schema, and every file in `envelope-invalid` and `payload-invalid` must fail.

```bash
# from cli/
uv run pytest tests/contract
```

Every schema guard needs one invalid fixture in `contracts/fixtures/payload-invalid` or `contracts/fixtures/envelope-invalid`. The fixture changes one leaf of its golden, so the guard is the only reason it fails. Name it `<type>.<rule>.json`.

## Mutation runs

`scripts/mutate.py`, `dotnet stryker` and `pnpm mutate` run locally only.

## Commits

One change per commit, with a short subject line and no body.

## License of contributions

jobranger is licensed under [PolyForm Noncommercial 1.0.0](LICENSE). By sending a contribution, you license it to the copyright holder, Nanthawat Dahl, under the MIT license, so it can ship in jobranger under its current license and any later one.
