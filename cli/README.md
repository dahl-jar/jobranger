# cli

C# CLI. Needs the .NET 10 SDK. Like the relay commands, these run from the repo root. Restore, format and build are in [CONTRIBUTING](../CONTRIBUTING.md#net).

```bash
dotnet test --project cli/tests/Jobranger.Cli.Tests -c Release --no-build

# one self-contained file per runtime: osx-arm64 or linux-x64
dotnet publish cli/src/Jobranger.Cli -c Release -r osx-arm64
cli/src/Jobranger.Cli/bin/Release/net10.0/osx-arm64/publish/jobranger --version

# image build and smoke run
docker build -f cli/deploy/Dockerfile -t jobranger-cli .
docker run --rm jobranger-cli --version

dotnet tool restore
(cd cli && dotnet stryker)
```

The published file unpacks its native libraries on first run into `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, or into `$HOME/.net` when the variable is unset. The image sets it to a 0700 folder under the state dir.

The test project points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and clears proxy variables and every other `JOBRANGER_*` variable. The opt-in switches `JOBRANGER_EMBEDDINGS_LIVE` and `JOBRANGER_EMBEDDINGS_CACHE` pass through. Test HTTP goes through `Mocks/LoopbackOnlyHandler`, which connects to loopback addresses only and follows no redirects.

The `Files/` tests and `ConfigFileTests` use real files, so they need macOS or Linux. `FileLock` takes a `flock` on the lock file, and `PrivateFile.OpenRead` reads the file type through the runtime's `SystemNative_FStat`.

`ArchitectureTests` checks the folder rules on the built assembly. Infrastructure folders use no feature, leaf features use infrastructure only, orchestration features and `Commands/` reach another feature through its entry type, and orchestration features call each other along the allowed edges only. Each rule also runs over the seed types in `ArchitectureSeeds/Broken`, which break it once, and `ArchitectureSeeds/Sound`, which pass every rule.
