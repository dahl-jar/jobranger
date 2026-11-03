#!/usr/bin/env bash
set -euo pipefail

image="jobranger-cli-tests:run-$$"
repo_root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"

docker build --file "$repo_root/dotnet/cli/deploy/test.Dockerfile" --tag "$image" "$repo_root"
trap 'docker image rm "$image" >/dev/null 2>&1 || true' EXIT
docker run --rm "$image" "$@"
