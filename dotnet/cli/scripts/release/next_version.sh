#!/usr/bin/env bash
set -euo pipefail

tag_prefix="cli-v"
initial_version="0.1.0"
feat_pattern='^feat(\([^)]*\))?:'
breaking_pattern='^[a-z]+(\([^)]*\))?!:'
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$#" -ne 2 ]; then
  echo "usage: next_version.sh <commit> <titles>" >&2
  exit 2
fi
commit="$1"
titles="$2"

bump_for() {
  if [[ "$1" =~ $breaking_pattern ]]; then
    echo breaking
  elif [[ "$1" =~ $feat_pattern ]]; then
    echo minor
  else
    echo patch
  fi
}

bump_for_titles() {
  local bump=patch line
  while IFS= read -r line; do
    case "$(bump_for "$line")" in
      breaking)
        echo breaking
        return
        ;;
      minor) bump=minor ;;
    esac
  done <<<"$1"
  echo "$bump"
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

latest_tag="$(bash "$script_dir/latest_tag.sh")"
if [ -z "$latest_tag" ]; then
  printf 'version=%s\nprevious=\n' "$initial_version"
  exit 0
fi
latest="${latest_tag#"$tag_prefix"}"

if git merge-base --is-ancestor "$commit" "$latest_tag"; then
  echo "commit $commit is already in $latest_tag" >&2
  printf 'version=\nprevious=\n'
  exit 0
fi

version="$(next "$latest" "$(bump_for_titles "$titles")")"
printf 'version=%s\nprevious=%s\n' "$version" "$latest_tag"
