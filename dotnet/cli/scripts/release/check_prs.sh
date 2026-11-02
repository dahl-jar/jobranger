#!/usr/bin/env bash
set -euo pipefail

waiver_label="release-checks-waived"
page_size=100
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$#" -ne 1 ]; then
  echo "usage: check_prs.sh <repository>" >&2
  exit 2
fi
repository="$1"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

failed=0

list_count() {
  jq -r --arg list "$2" '"\(.[$list] | length) \(.total_count)"' "$1"
}

partial() {
  echo "PR #$1: GitHub returned $2 of $3 $4, and the gate reads at most $page_size" >&2
  failed=1
}

while IFS=$'\t' read -r number head waived _title; do
  if [ "$waived" = true ]; then
    echo "PR #$number: checks waived by the $waiver_label label"
    continue
  fi
  gh api "repos/$repository/actions/runs?head_sha=$head&per_page=$page_size" >"$tmp/runs.json" </dev/null
  gh api "repos/$repository/commits/$head/check-runs?filter=latest&per_page=$page_size" >"$tmp/checks.json" </dev/null
  checks_count="$(list_count "$tmp/checks.json" check_runs)"
  runs_count="$(list_count "$tmp/runs.json" workflow_runs)"
  read -r checks_returned checks_total <<<"$checks_count"
  read -r runs_returned runs_total <<<"$runs_count"
  if [ "$checks_total" -gt "$checks_returned" ]; then
    partial "$number" "$checks_returned" "$checks_total" "check runs"
    continue
  fi
  if [ "$runs_total" -gt "$runs_returned" ]; then
    partial "$number" "$runs_returned" "$runs_total" "workflow runs"
    continue
  fi
  if [ "$checks_returned" -eq 0 ]; then
    echo "PR #$number: no check runs found"
    continue
  fi
  lines="$(jq -rn --slurpfile runs "$tmp/runs.json" --slurpfile checks "$tmp/checks.json" -f "$script_dir/not_green.jq")"
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    echo "PR #$number: $line" >&2
    failed=1
  done <<<"$lines"
done

if [ "$failed" -ne 0 ]; then
  echo "The checks above did not pass. Wait for running checks to finish, re-run failed checks, or add the $waiver_label label to the pull request. Then start a new Release CLI run." >&2
  exit 1
fi
