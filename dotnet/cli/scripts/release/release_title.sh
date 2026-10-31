#!/usr/bin/env bash
set -euo pipefail

merged_title_query='[.[] | select(.merged_at != null)][0].title // empty'

if [ "$#" -ne 3 ]; then
  echo "usage: release_title.sh <repository> <commit> <pr-title>" >&2
  exit 2
fi
repository="$1"
commit="$2"
title="$3"

if [ -z "$title" ]; then
  title="$(gh api "repos/$repository/commits/$commit/pulls" --jq "$merged_title_query")"
fi
if [ -z "$title" ]; then
  title="$(git log -1 --format=%s "$commit")"
fi
printf '%s\n' "$title"
