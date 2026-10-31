# relay

Needs the .NET 10 SDK. Commands run from `dotnet/`. Restore, format and build are in [CONTRIBUTING](../../CONTRIBUTING.md#net).

```bash
dotnet test --project relay/tests/Jobranger.Relay.Core.Tests -c Release --no-build
dotnet test --project relay/tests/Jobranger.Relay.Tests -c Release --no-build
dotnet tool restore
(cd relay && dotnet stryker)
```
