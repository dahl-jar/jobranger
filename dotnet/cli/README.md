# cli

C# CLI. Needs the .NET 10 SDK. Like the relay commands, these run from `dotnet/`. Restore, format and build are in [CONTRIBUTING](../../CONTRIBUTING.md#net).

```bash
dotnet test --project cli/tests/Jobranger.Cli.Tests -c Release --no-build

# the same tests in a Linux container
bash cli/scripts/container/test_in_container.sh

# one self-contained file for linux-x64
dotnet publish cli/src/Jobranger.Cli -c Release -r linux-x64

# image build and smoke run
bash cli/deploy/build_image.sh cli/src/Jobranger.Cli/bin/Release/net10.0/linux-x64/publish/jobranger jobranger-cli
docker run --rm --platform linux/amd64 jobranger-cli --version

dotnet tool restore
(cd cli && dotnet stryker)
```

The published file unpacks its native libraries on first run into `DOTNET_BUNDLE_EXTRACT_BASE_DIR`, or into `$HOME/.net` when the variable is unset. The image sets it to a 0700 folder under the state dir.

The test project points `HOME`, the `XDG_*` dirs and the `JOBRANGER_*` dirs at a temp folder and clears proxy variables and every other `JOBRANGER_*` variable. The opt-in switches `JOBRANGER_EMBEDDINGS_LIVE` and `JOBRANGER_EMBEDDINGS_CACHE` pass through. Test HTTP goes through `Mocks/LoopbackOnlyHandler`, which connects to loopback addresses only and follows no redirects.

The `Files/` tests and `ConfigFileTests` need Linux, because they use real files. On macOS, run them with `cli/scripts/container/test_in_container.sh`. `FileLock` takes a `flock` on the lock file, and `PrivateFile.OpenRead` reads the file type through the runtime's `SystemNative_FStat`.

`ArchitectureTests` checks the folder rules on the built assembly. Infrastructure folders use no feature, leaf features use infrastructure only, orchestration features and `Commands/` reach another feature through its entry type, and orchestration features call each other along the allowed edges only. Each rule also runs over the seed types in `ArchitectureSeeds/Broken`, which break it once, and `ArchitectureSeeds/Sound`, which pass every rule.

## release

A release starts when someone runs `.github/workflows/release-cli.yml` on `main`, from the Actions tab or with `gh workflow run release-cli.yml --ref main`. The run first checks that every pull request merged since the last `cli-v*` tag passed its checks. It then builds the CLI for linux-x64 and publishes a GitHub release tagged `cli-vX.Y.Z` with the archive, `SHA256SUMS` and `install.sh`. It also pushes the image `ghcr.io/dahl-jar/jobranger/cli` with the tags `X.Y.Z` and `latest`, built from the binary in the archive. The version comes from the tag, passed to `dotnet publish -p:Version=`, and the csproj keeps `0.1.0` for local builds. To run every step except the publish, add `-f dry-run=true`. A dry run builds the image and pushes nothing. To release past a commit on `main` that has no pull request, add `-f accepted-commits="<full sha> ..."`.

The titles of the pull requests since the last tag pick the bump. A `feat:` title raises the minor version, a `!` before the colon raises the minor version before 1.0 and the major version after it, and with neither the patch version goes up. A run whose commit is already in a release publishes nothing.

A run stops before it publishes when `main` has a newer commit than the run, and the message says to start a new run. A run also stops when a merged pull request has a check that did not pass, and it names the pull request and the check. Re-run that check, or add the `release-checks-waived` label to the pull request, then start a new run.

```bash
bash cli/scripts/release/tests/release_test.sh
```

## docker

There is no macOS build. On macOS, run the image `ghcr.io/dahl-jar/jobranger/cli` with Docker Desktop. On Linux, install the archive with `install.sh`.

On Apple Silicon, turn on `Use Rosetta for x86_64/amd64 emulation on Apple Silicon` in the Docker Desktop settings, under General. The image is built for amd64. Without Rosetta, Docker Desktop uses QEMU, and .NET does not run under QEMU.

```bash
mkdir -p "$HOME/.config/jobranger"
docker run --rm -it --platform linux/amd64 \
  -v "$HOME/.config/jobranger:/home/app/.config/jobranger" \
  -v jobranger:/home/app/.local \
  -e LANG \
  ghcr.io/dahl-jar/jobranger/cli init
```

Replace `init` with `run`, `pair`, `report` or any other command. The prompts need `-it`. With `-e LANG`, the CLI uses the host language. With `LANG` unset on the host, it uses English, unless `config.yaml` sets the language.

- Config: `config.yaml`, `profile.yaml`, `context.yaml` and `guidance.md` are in `~/.config/jobranger` on the host.
- Secrets: the image has no keyring, and `init` saves the API keys to `secrets.json` in the same folder. Keys in environment variables, such as `-e ANTHROPIC_API_KEY`, also work.
- Data and state: the database, logs, locks and the folder where the CLI unpacks its native libraries are in the `jobranger` volume.
- Editor: the image has no editor, and `config edit`, `guidance edit` and `about edit` stop with an error. Edit the files in `~/.config/jobranger` on the host.
- Updates: `docker pull ghcr.io/dahl-jar/jobranger/cli`.

An installed macOS build prints the `install.sh` command in its next update notice. That command now stops with a message that points here.

To keep the database of the macOS build, run the command above once, then copy the database into the volume:

```bash
docker run --rm \
  -v "$HOME/Library/Application Support/jobranger:/old:ro" \
  -v jobranger:/data \
  alpine sh -c 'mkdir -p /data/share/jobranger && cp /old/jobranger.db* /data/share/jobranger/ && chown -R 1654:1654 /data/share'
```

The container cannot read the macOS Keychain. Run `init` again to enter the keys.
