# Contributing

Each app has a README.md with its build and test commands:

- [cli](dotnet/cli/README.md): the C# CLI, its Docker image, goldens and release scripts
- [cli-python](cli-python/README.md): the Python CLI the goldens are exported from
- [relay](dotnet/relay/README.md): the C# relay
- [frontend](frontend/README.md): the frontend
- [gateway](gateway/README.md): the Cloudflare Worker in front of the relay
- [messages](messages/README.md): the schemas, fixtures and codegen every app tests against. The C# messaging library is in `dotnet/messages`.

The root holds what more than one app uses: CI in `.github`, `.editorconfig`, `.gitattributes` and `.gitignore`. `dotnet/` holds cli, relay, the C# messaging library and their shared build files.

The tests of each app run with the commands in its README. [messages](messages/README.md) has the commands that regenerate the message code and check it for drift.

## .NET

In `dotnet/`, cli, relay and messages share `Jobranger.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config` and `.config/dotnet-tools.json`. Needs the .NET 10 SDK. Run these from `dotnet/`, because `global.json` is only found from there:

```bash
dotnet restore Jobranger.slnx --locked-mode
dotnet format Jobranger.slnx --verify-no-changes --no-restore
dotnet build Jobranger.slnx -c Release --no-restore -warnaserror
dotnet test --solution Jobranger.slnx -c Release --no-build
dotnet tool restore
```

Each .NET project has a committed `packages.lock.json`. After a package change in `Directory.Packages.props`, run `dotnet restore Jobranger.slnx --force-evaluate` and commit the updated lock files with it.

## Mutation runs

`cli-python/scripts/mutate.py`, `dotnet stryker` and `pnpm mutate` (frontend and gateway) run locally only.

## Commits

One change per commit, with a short subject line and no body.

## License of contributions

jobranger is licensed under [PolyForm Noncommercial 1.0.0](LICENSE). By sending a contribution, you license it to the copyright holder, Nanthawat Dahl, under the MIT license, so it can ship in jobranger under its current license and any later one.
