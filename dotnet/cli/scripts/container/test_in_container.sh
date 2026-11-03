#!/usr/bin/env bash
set -euo pipefail

image="jobranger-cli-tests"
repo_root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"

docker build --file "$repo_root/dotnet/cli/deploy/test.Dockerfile" --tag "$image" "$repo_root"
exec docker run --rm "$image" "$@"
