# Contributing

Each app has a README.md with its build and test commands:

- [cli](cli/README.md): the C# CLI, its Docker image, goldens and release scripts
- [cli-python](cli-python/README.md): the Python CLI the goldens are exported from
- [relay](relay/README.md): the C# relay
- [web](web/README.md): the frontend
- [edge](edge/README.md): the Cloudflare Worker in front of the relay
- [contracts](contracts/README.md): the schemas, fixtures and codegen every app tests against, and the C# contracts library

The root holds what more than one app uses: CI in `.github`, `mise.toml`, `.editorconfig`, `.gitattributes`, `.gitignore` and the .NET build files.

With [mise](https://mise.jdx.dev) installed, `mise run test` runs every suite from the repo root and `mise run contracts:check` checks the generated contract code. `mise.toml` pins the tool versions.

## .NET

cli, relay and contracts share `Jobranger.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config` and `.config/dotnet-tools.json`. Needs the .NET 10 SDK. Run these from the repo root, because `global.json` is only found from there:

```bash
dotnet restore Jobranger.slnx --locked-mode
dotnet format Jobranger.slnx --verify-no-changes --no-restore
dotnet build Jobranger.slnx -c Release --no-restore -warnaserror
dotnet test --solution Jobranger.slnx -c Release --no-build
dotnet tool restore
```

Each .NET project has a committed `packages.lock.json`. After a package change in `Directory.Packages.props`, run `dotnet restore Jobranger.slnx --force-evaluate` and commit the updated lock files with it.

## Mutation runs

`cli-python/scripts/mutate.py`, `dotnet stryker` and `pnpm mutate` (web and edge) run locally only.

## Commits

One change per commit, with a short subject line and no body.

## License of contributions

jobranger is licensed under [PolyForm Noncommercial 1.0.0](LICENSE). By sending a contribution, you license it to the copyright holder, Nanthawat Dahl, under the MIT license, so it can ship in jobranger under its current license and any later one.
