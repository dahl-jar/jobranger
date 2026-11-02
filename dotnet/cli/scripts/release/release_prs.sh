#!/usr/bin/env bash
set -euo pipefail

waiver_label="release-checks-waived"
merged_pr_query="[.[] | select(.merged_at != null)][0] // empty | [.number, .head.sha, ([.labels[].name] | index(\"$waiver_label\") != null), .title] | @tsv"

if [ "$#" -ne 4 ]; then
  echo "usage: release_prs.sh <repository> <previous-tag> <commit> <accepted-commits>" >&2
  exit 2
fi
repository="$1"
previous="$2"
commit="$3"
accepted="$4"
range="${previous:+$previous..}$commit"

commits="$(git rev-list --reverse "$range")"
failed=0
while IFS= read -r sha; do
  [ -n "$sha" ] || continue
  row="$(gh api "repos/$repository/commits/$sha/pulls" --jq "$merged_pr_query" </dev/null)"
  if [ -n "$row" ]; then
    printf '%s\n' "$row"
    continue
  fi
  case " $accepted " in
    *" $sha "*)
      echo "commit $sha accepted without a pull request by the run input" >&2
      ;;
    *)
      echo "commit $sha has no merged pull request" >&2
      failed=1
      ;;
  esac
done <<<"$commits"

for sha in $accepted; do
  if ! grep -Fqx "$sha" <<<"$commits"; then
    echo "accepted commit $sha is not in $range" >&2
    failed=1
  fi
done

exit "$failed"
