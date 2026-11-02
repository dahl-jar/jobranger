#!/usr/bin/env bash
set -euo pipefail

removal_label="tests-removed"

if [ "$#" -ne 3 ]; then
  echo "usage: missing_tests.sh <base-list> <head-list> <allow-removal>" >&2
  exit 2
fi
base_list="$1"
head_list="$2"
allow_removal="$3"

names() {
  sed -n 's/^  //p' "$1" | LC_ALL=C sort
}

count() {
  printf '%s\n' "$1" | wc -l | tr -d ' '
}

list() {
  local name
  while IFS= read -r name; do
    printf '%s: %s\n' "$1" "$name"
  done <<<"$2"
}

base_names="$(names "$base_list")"
head_names="$(names "$head_list")"
if [ -z "$base_names" ]; then
  echo "found no tests in $base_list" >&2
  exit 1
fi

missing="$(LC_ALL=C comm -23 <(printf '%s\n' "$base_names") <(printf '%s\n' "$head_names"))"
if [ -z "$missing" ]; then
  echo "kept all $(count "$base_names") tests from the base"
  exit 0
fi

if [ "$allow_removal" = true ]; then
  list removed "$missing"
  echo "accepted $(count "$missing") removed tests because the pull request has the $removal_label label"
  exit 0
fi

list missing "$missing" >&2
echo "$(count "$missing") tests from the base are missing. Restore them, or add the $removal_label label to the pull request and re-run this job." >&2
exit 1
