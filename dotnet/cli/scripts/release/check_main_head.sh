#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: check_main_head.sh <repository> <commit>" >&2
  exit 2
fi
repository="$1"
commit="$2"

head="$(gh api "repos/$repository/git/ref/heads/main" --jq .object.sha)"
if [ "$head" != "$commit" ]; then
  echo "main is at $head, and this run releases $commit. Start a new Release CLI run to release the head of main." >&2
  exit 1
fi
