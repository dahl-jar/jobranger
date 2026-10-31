#!/usr/bin/env bash
set -euo pipefail

tag_prefix="cli-v"
version_pattern='[0-9]+\.[0-9]+\.[0-9]+'
initial_version="0.1.0"
feat_pattern='^feat(\([^)]*\))?:'
breaking_pattern='^[a-z]+(\([^)]*\))?!:'

if [ "$#" -ne 2 ]; then
  echo "usage: next_version.sh <commit> <title>" >&2
  exit 2
fi
commit="$1"
title="$2"

latest_version() {
  git tag --list "${tag_prefix}*" \
    | sed -n -E "s/^${tag_prefix}(${version_pattern})\$/\\1/p" \
    | sort -t. -k1,1n -k2,2n -k3,3n \
    | tail -n 1
}

bump_for() {
  if [[ "$1" =~ $breaking_pattern ]]; then
    echo breaking
  elif [[ "$1" =~ $feat_pattern ]]; then
    echo minor
  else
    echo patch
  fi
}

next() {
  local major minor patch
  IFS=. read -r major minor patch <<<"$1"
  case "$2" in
    breaking)
      if [ "$((10#$major))" -eq 0 ]; then
        echo "0.$((10#$minor + 1)).0"
      else
        echo "$((10#$major + 1)).0.0"
      fi
      ;;
    minor) echo "$((10#$major)).$((10#$minor + 1)).0" ;;
    patch) echo "$((10#$major)).$((10#$minor)).$((10#$patch + 1))" ;;
  esac
}

latest="$(latest_version)"
if [ -z "$latest" ]; then
  printf 'version=%s\nprevious=\n' "$initial_version"
  exit 0
fi

latest_tag="$tag_prefix$latest"
if git merge-base --is-ancestor "$commit" "$latest_tag"; then
  echo "commit $commit is already in $latest_tag" >&2
  printf 'version=\nprevious=\n'
  exit 0
fi

version="$(next "$latest" "$(bump_for "$title")")"
printf 'version=%s\nprevious=%s\n' "$version" "$latest_tag"
