# messages

Commands run from the repo root.

`messages/generate.sh` writes the pydantic models, the TypeScript types, the Ajv validators and the schema copies from `messages/payload.schema.json` and `messages/envelope.schema.json`. Generated code must match the schemas.

```bash
bash messages/generate.sh
git diff --exit-code
```

All three stacks test the same fixtures. Every golden in `messages/fixtures/envelope` and `messages/fixtures/payload` must pass its schema, and every file in `envelope-invalid` and `payload-invalid` must fail.

The cli-python tests cover the schemas, decoders, limits and fixtures:

```bash
uv --directory cli-python run pytest tests/contract
```

The frontend tests cover the validators, from source and from the built bundle:

```bash
pnpm --dir frontend exec vitest run src/features/relay/contract tests
```

The C# messaging library tests cover the envelope rules, proofs, limits, the payload codec and the URL, timestamp, email and base64 field helpers:

```bash
(cd dotnet && dotnet test --project messages/tests/Jobranger.Messaging.Tests -c Release)
```

Every schema guard needs one invalid fixture in `messages/fixtures/payload-invalid` or `messages/fixtures/envelope-invalid`. The fixture changes one leaf of its golden, so the guard is the only reason it fails. Name it `<type>.<rule>.json`. A new fixture also raises the counts in `frontend/src/test/fixtureCounts.ts`. An envelope fixture raises the count in `ShouldFindEveryInvalidFixture` in `Jobranger.Messaging.Tests`, and a payload fixture raises the counts in `ShouldFindEveryPayloadFixture_WhenFixturesAreLinked`.

`messages/unicode/idna-hosts-16.0.0.json` holds the non-ASCII code points `Urls.Encode` accepts in a host, plus the UTS 46 mapping and NFC data. It is generated from the Unicode 16.0.0 data files, and a rerun gives the same file:

```bash
python3 messages/scripts/idna_hosts.py
```

`url_parity.py` writes `messages/fixtures/urls/parity.json`, the Python results `UrlParityTests` compares against. CI reruns it and fails on a diff:

```bash
python3 messages/scripts/url_parity.py
```

Mutation runs scoped to the contract code:

```bash
(cd cli-python && uv run python scripts/mutate.py "jobranger.contract*")
(cd frontend && pnpm mutate --mutate "src/features/relay/contract/*.ts,!src/features/relay/contract/*.test.ts,!src/features/relay/contract/*.gen.*")
(cd dotnet/messages && dotnet stryker)
(cd dotnet/messages && dotnet stryker --mutate "**/EnvelopeRules.cs")
(cd dotnet/messages && dotnet stryker --mutate "**/Payloads/*.cs" --mutate "**/Fields/*.cs")
```
