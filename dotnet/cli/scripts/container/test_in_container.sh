#!/usr/bin/env bash
set -euo pipefail

image="jobranger-cli-tests"
image_label="jobranger.image=$image"
repo_root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"

docker build --file "$repo_root/dotnet/cli/deploy/test.Dockerfile" --label "$image_label" --tag "$image" "$repo_root"
docker image prune --force --filter "label=$image_label"
exec docker run --rm "$image" "$@"
