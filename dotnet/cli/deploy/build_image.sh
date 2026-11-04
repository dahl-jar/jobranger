#!/usr/bin/env bash
set -euo pipefail

platform="linux/amd64"
deploy_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$#" -lt 2 ]; then
  echo "usage: build_image.sh <binary> <tag>..." >&2
  exit 2
fi
binary="$1"
shift

context="$(mktemp -d)"
trap 'rm -rf "$context"' EXIT

state_dir="$context/home/.local/state/jobranger"
cp "$binary" "$context/jobranger"
chmod 0755 "$context/jobranger"
mkdir -p "$context/home/.config" "$state_dir/bundle"
chmod 0700 "$state_dir" "$state_dir/bundle"

tags=()
for tag in "$@"; do
  tags+=(-t "$tag")
done

docker build --platform "$platform" -f "$deploy_dir/Dockerfile" "${tags[@]}" "$context"
