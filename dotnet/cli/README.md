# cli

C# CLI. Needs the .NET 10 SDK. Like the relay commands, these run from `dotnet/`. Restore, format and build are in [CONTRIBUTING](../../CONTRIBUTING.md#net).

```bash
dotnet test --project cli/tests/Jobranger.Cli.Tests -c Release --no-build

# one self-contained file per runtime: osx-arm64 or linux-x64
dotnet publish cli/src/Jobranger.Cli -c Release -r osx-arm64
cli/src/Jobranger.Cli/bin/Release/net10.0/osx-arm64/publish/jobranger --version

# image build and smoke run
(cd .. && docker build -f dotnet/cli/deploy/Dockerfile -t jobranger-cli .)
docker run --rm jobranger-cli --version

dotnet tool restore
(cd cli && dotnet stryker)
```

The published file unpacks its native libraries on first run into `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, or into `$HOME/.net` when the variable is unset. The image sets it to a 0700 folder under the state dir.

The test project points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and clears proxy variables and every other `JOBRANGER_*` variable. The opt-in switches `JOBRANGER_EMBEDDINGS_LIVE` and `JOBRANGER_EMBEDDINGS_CACHE` pass through. Test HTTP goes through `Mocks/LoopbackOnlyHandler`, which connects to loopback addresses only and follows no redirects.

The `Files/` tests and `ConfigFileTests` use real files, so they need macOS or Linux. `FileLock` takes a `flock` on the lock file, and `PrivateFile.OpenRead` reads the file type through the runtime's `SystemNative_FStat`.

`ArchitectureTests` checks the folder rules on the built assembly. Infrastructure folders use no feature, leaf features use infrastructure only, orchestration features and `Commands/` reach another feature through its entry type, and orchestration features call each other along the allowed edges only. Each rule also runs over the seed types in `ArchitectureSeeds/Broken`, which break it once, and `ArchitectureSeeds/Sound`, which pass every rule.

## release

A release starts when someone runs `.github/workflows/release-cli.yml` on `main`, from the Actions tab or with `gh workflow run release-cli.yml --ref main`. The run first checks that every pull request merged since the last `cli-v*` tag passed its checks. It then builds the CLI for osx-arm64 and linux-x64 and publishes a GitHub release tagged `cli-vX.Y.Z` with the archives, `SHA256SUMS` and `install.sh`. The version comes from the tag, passed to `dotnet publish -p:Version=`, and the csproj keeps `0.1.0` for local builds. To run every step except the publish, add `-f dry-run=true`. To release past a commit on `main` that has no pull request, add `-f accepted-commits="<full sha> ..."`.

The titles of the pull requests since the last tag pick the bump. A `feat:` title raises the minor version, a `!` before the colon raises the minor version before 1.0 and the major version after it, and with neither the patch version goes up. A run whose commit is already in a release publishes nothing.

A run stops before it publishes when `main` has a newer commit than the run, and the message says to start a new run. A run also stops when a merged pull request has a check that did not pass, and it names the pull request and the check. Re-run that check, or add the `release-checks-waived` label to the pull request, then start a new run.

```bash
bash cli/scripts/release/tests/release_test.sh
```
