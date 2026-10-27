# contracts

Commands run from the repo root.

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

`contracts/unicode/idna-hosts-16.0.0.json` holds the non-ASCII code points `Urls.Encode` accepts in a host, plus the UTS 46 mapping and NFC data. It is generated from the Unicode 16.0.0 data files, and a rerun gives the same file:

```bash
python3 contracts/scripts/idna_hosts.py
```

`url_parity.py` writes `contracts/fixtures/urls/parity.json`, the Python results `UrlParityTests` compares against. CI reruns it and fails on a diff:

```bash
python3 contracts/scripts/url_parity.py
```

Mutation runs scoped to the contract code:

```bash
(cd cli-python && uv run python scripts/mutate.py "jobranger.contract*")
(cd web && pnpm mutate --mutate "src/features/relay/contract/*.ts,!src/features/relay/contract/*.test.ts,!src/features/relay/contract/*.gen.*")
(cd contracts && dotnet stryker)
(cd contracts && dotnet stryker --mutate "**/EnvelopeRules.cs")
(cd contracts && dotnet stryker --mutate "**/Payloads/*.cs" --mutate "**/Fields/*.cs")
```
