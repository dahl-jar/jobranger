# relay

Needs the .NET 10 SDK. Commands run from `dotnet/`. Restore, format and build are in [CONTRIBUTING](../../CONTRIBUTING.md#net).

```bash
dotnet test --project relay/tests/Jobranger.Relay.Core.Tests -c Release --no-build
dotnet test --project relay/tests/Jobranger.Relay.Tests -c Release --no-build
bash relay/scripts/release/tests/pin_image_test.sh
dotnet tool restore
(cd relay && dotnet stryker)
```

## release

A push to `main` that changes the relay runs `.github/workflows/release-relay.yml`. It calls `relay.yml`, and when the checks pass it pushes `ghcr.io/dahl-jar/jobranger/relay` with the tags `sha-<commit>` and `latest`. It then writes the tag and digest to `versions/jobranger-relay` in the manifests repo, and Argo rolls out that image.

The workflow needs the secrets `MANIFESTS_REPO` and `MANIFESTS_DEPLOY_KEY`.
