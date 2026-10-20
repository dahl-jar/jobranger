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

The test suite points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and swaps the keyring for an in-memory one, so a test run leaves the real config and keychain alone.

The default matching prompt is `PROMPT_HEADER` and `BASE_RULES` in `src/jobranger/matching/prompt.py`. The tests compare it byte for byte with `tests/fixtures/main_prompt_header.txt` and `tests/fixtures/main_base_rules.txt`, so a prompt change updates both fixtures in the same commit. Any prompt change also changes the assessment version, and every stored ad is checked again on the next run.

After a prompt change, re-record the model replies in `tests/fixtures/matching_replies.json` that the matching-case tests read.

```bash
# one model call per case in tests/fixtures/matching_cases.json
uv run python scripts/record_matching_replies.py claude -p --model claude-sonnet-5-5 --output-format json --tools ""
```

CV extraction parses PDF and DOCX files in a child process (`python -I -m jobranger.cv_worker`). The mutation run only sees code that runs inside pytest, so `tests/test_cv_worker.py` calls the worker logic in-process. `tests/test_cv.py` covers the child process path, with the stand-in workers in `tests/fake_cv_workers.py`.

`tests/fixtures/cv/cv_user.pdf` is built from `cv_user.typ`. `cv_user_scan.pdf` is page 1 of it as a 40 ppi image with no text layer.

```bash
# from tests/fixtures/cv
typst compile cv_user.typ cv_user.pdf
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
