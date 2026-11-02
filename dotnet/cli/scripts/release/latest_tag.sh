#!/usr/bin/env bash
set -euo pipefail

tag_prefix="cli-v"
version_pattern='[0-9]+\.[0-9]+\.[0-9]+'

if [ "$#" -ne 0 ]; then
  echo "usage: latest_tag.sh" >&2
  exit 2
fi

latest="$(git tag --list "${tag_prefix}*" \
  | sed -n -E "s/^${tag_prefix}(${version_pattern})\$/\\1/p" \
  | sort -t. -k1,1n -k2,2n -k3,3n \
  | tail -n 1)"
if [ -n "$latest" ]; then
  printf '%s%s\n' "$tag_prefix" "$latest"
fi
