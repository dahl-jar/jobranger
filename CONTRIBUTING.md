# Contributing

Each component builds and tests on its own. The cli-python, web and edge commands run from their component folder. The relay and contracts commands run from the repo root.

With [mise](https://mise.jdx.dev) installed, `mise run test` runs every suite from the repo root and `mise run contracts:check` checks the generated contract code. `mise.toml` pins the tool versions.

## cli

C# CLI. Needs the .NET 10 SDK. Like the relay commands, these run from the repo root.

```bash
dotnet restore Jobranger.slnx --locked-mode
dotnet format Jobranger.slnx --verify-no-changes --no-restore
dotnet build Jobranger.slnx -c Release --no-restore -warnaserror
dotnet test --project cli/tests/Jobranger.Cli.Tests -c Release --no-build

# one self-contained file per runtime: osx-arm64 or linux-x64
dotnet publish cli/src/Jobranger.Cli -c Release -r osx-arm64
cli/src/Jobranger.Cli/bin/Release/net10.0/osx-arm64/publish/jobranger --version

# image build and smoke run
docker build -f deploy/cli/Dockerfile -t jobranger-cli .
docker run --rm jobranger-cli --version

dotnet tool restore
(cd cli && dotnet stryker)
```

The published file unpacks its native libraries on first run into `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, or into `$HOME/.net` when the variable is unset. The image sets it to a 0700 folder under the state dir.

The test project points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and clears proxy variables and every other `JOBRANGER_*` variable. The opt-in switches `JOBRANGER_EMBEDDINGS_LIVE` and `JOBRANGER_EMBEDDINGS_CACHE` pass through. Test HTTP goes through `Mocks/LoopbackOnlyHandler`, which connects to loopback addresses only and follows no redirects.

The `Files/` tests and `ConfigFileTests` use real files, so they need macOS or Linux. `FileLock` takes a `flock` on the lock file, and `PrivateFile.OpenRead` reads the file type through the runtime's `SystemNative_FStat`.

`ArchitectureTests` checks the folder rules on the built assembly. Infrastructure folders use no feature, leaf features use infrastructure only, orchestration features and `Commands/` reach another feature through its entry type, and orchestration features call each other along the allowed edges only. Each rule also runs over the seed types in `ArchitectureSeeds/Broken`, which break it once, and `ArchitectureSeeds/Sound`, which pass every rule.

## cli-python

Needs Python 3.12+ and uv.

```bash
uv sync --locked
uv run ruff check
uv run ruff format --check
uv run mypy
uv run pytest
uv run python scripts/mutate.py
```

The test suite points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and swaps the keyring for an in-memory one, so a test run leaves the real config and keychain alone.

The default matching prompt is `PROMPT_HEADER` and `BASE_RULES` in `src/jobranger/matching/prompt.py`. The tests compare it byte for byte with `tests/fixtures/main_prompt_header.txt` and `tests/fixtures/main_base_rules.txt`, so a prompt change updates both fixtures in the same commit. Any prompt change also changes the assessment version, and every stored ad is checked again on the next run.

After a prompt change, re-record the model replies in `tests/fixtures/matching_replies.json` that the matching-case tests read. The script makes one model call per case in `tests/fixtures/matching_cases.json`.

```bash
uv run python scripts/record_matching_replies.py claude -p --model claude-sonnet-5-5 --output-format json --tools ""
```

CV extraction parses PDF and DOCX files in a child process (`python -I -m jobranger.cv_worker`). The mutation run only sees code that runs inside pytest, so `tests/test_cv_worker.py` calls the worker logic in-process. `tests/test_cv.py` covers the child process path, with the stand-in workers in `tests/fake_cv_workers.py`.

`tests/fixtures/cv/cv_user.pdf` is built from `cv_user.typ`. `cv_user_scan.pdf` is page 1 of it as a 40 ppi image with no text layer.

```bash
typst compile tests/fixtures/cv/cv_user.typ tests/fixtures/cv/cv_user.pdf
```

Tests can only connect to loopback. Any other host fails with `NetworkBlockedError`, so stub HTTP with `httpx.MockTransport` or `httpx2.MockTransport`.

## relay

Needs the .NET 10 SDK.

```bash
dotnet restore Jobranger.slnx --locked-mode
dotnet format Jobranger.slnx --verify-no-changes --no-restore
dotnet build Jobranger.slnx -c Release --no-restore -warnaserror
dotnet test --solution Jobranger.slnx -c Release --no-build
dotnet tool restore
(cd relay && dotnet stryker)
(cd contracts && dotnet stryker)
```

Each .NET project has a committed `packages.lock.json`. After a package change in `Directory.Packages.props`, run `dotnet restore Jobranger.slnx --force-evaluate` and commit the updated lock files with it.

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

Dev server with hot reload:

```bash
pnpm dev
```

Rebuild the subset fonts in `public/fonts` from the pinned Nerd Fonts release:

```bash
pnpm fonts
```

## edge

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

## contracts

`contracts/generate.sh` writes the pydantic models, the TypeScript types, the Ajv validators and the schema copies from `contracts/payload.schema.json` and `contracts/envelope.schema.json`. Generated code must match the schemas.

```bash
bash contracts/generate.sh
git diff --exit-code
```

All three stacks test the same fixtures. Every golden in `contracts/fixtures/envelope` and `contracts/fixtures/payload` must pass its schema, and every file in `envelope-invalid` and `payload-invalid` must fail.

The cli-python tests cover the schemas, decoders, limits and fixtures:

```bash
uv --directory cli-python run pytest tests/contract
```

The web tests cover the validators, from source and from the built bundle:

```bash
pnpm --dir web exec vitest run src/features/relay/contract tests
```

The C# contracts library tests cover the envelope rules, proofs, limits, the payload codec and the URL, timestamp, email and base64 field helpers:

```bash
dotnet test --project contracts/tests/Jobranger.Contracts.Tests -c Release
```

Every schema guard needs one invalid fixture in `contracts/fixtures/payload-invalid` or `contracts/fixtures/envelope-invalid`. The fixture changes one leaf of its golden, so the guard is the only reason it fails. Name it `<type>.<rule>.json`. A new fixture also raises the counts in `web/src/test/fixtureCounts.ts`. An envelope fixture raises the count in `ShouldFindEveryInvalidFixture` in the contracts tests, and a payload fixture raises the counts in `ShouldFindEveryPayloadFixture_WhenFixturesAreLinked`.

`contracts/unicode/idna-hosts-16.0.0.json` lists the non-ASCII code points `Urls.Encode` accepts in a host. It is generated from the Unicode 16.0.0 data files, and a rerun gives the same file:

```bash
python3 scripts/unicode/idna_hosts.py
```

Mutation runs scoped to the contract code:

```bash
(cd cli-python && uv run python scripts/mutate.py "jobranger.contract*")
(cd web && pnpm mutate --mutate "src/features/relay/contract/*.ts,!src/features/relay/contract/*.test.ts,!src/features/relay/contract/*.gen.*")
(cd contracts && dotnet stryker --mutate "**/EnvelopeRules.cs")
(cd contracts && dotnet stryker --mutate "**/Payloads/*.cs" --mutate "**/Fields/*.cs")
```

## Mutation runs

`cli-python/scripts/mutate.py`, `dotnet stryker` and `pnpm mutate` (web and edge) run locally only.

## Commits

One change per commit, with a short subject line and no body.

## License of contributions

jobranger is licensed under [PolyForm Noncommercial 1.0.0](LICENSE). By sending a contribution, you license it to the copyright holder, Nanthawat Dahl, under the MIT license, so it can ship in jobranger under its current license and any later one.
