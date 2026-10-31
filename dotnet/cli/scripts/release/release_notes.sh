#!/usr/bin/env bash
set -euo pipefail

section_heading='## What does this PR do?'
merged_pr_query='[.[] | select(.merged_at != null)][0] // empty | "\(.number)\n\(.title)\n\(.body)"'

section_of() {
  tr -d '\r' | awk -v heading="$section_heading" '
    {
      line = $0
      sub(/[ \t]+$/, "", line)
      if (line ~ /^[ \t]*(```|~~~)/) {
        fenced = !fenced
      } else if (!fenced && line ~ /^## /) {
        if (in_section) { done = 1 }
        if (line == heading) { in_section = 1; next }
      }
      if (!in_section || done) { next }
      if (line == "" && !fenced) {
        if (started) { blanks++ }
        next
      }
      while (blanks > 0) { print ""; blanks-- }
      print
      started = 1
    }
  '
}

if [ "$#" -ne 4 ]; then
  echo "usage: release_notes.sh <repository> <commit> <tag> <previous-tag>" >&2
  exit 2
fi
repository="$1"
commit="$2"
tag="$3"
previous="$4"

range="$commit"
if [ -n "$previous" ]; then
  range="$previous..$commit"
fi
commits="$(git log --reverse --format=%H "$range")"

seen=" "
for sha in $commits; do
  pr="$(gh api "repos/$repository/commits/$sha/pulls" --jq "$merged_pr_query")"
  if [ -z "$pr" ]; then
    echo "no merged PR for commit $sha, using its subject" >&2
    printf -- '- %s\n\n' "$(git log -1 --format=%s "$sha")"
    continue
  fi
  { read -r number; read -r title; body="$(cat)"; } <<<"$pr"
  case "$seen" in
    *" $number "*) continue ;;
  esac
  seen="$seen$number "
  section="$(printf '%s\n' "$body" | section_of)"
  if [ -z "$section" ]; then
    echo "PR $number has no text under $section_heading, using its title" >&2
    section="- $title"
  fi
  printf '%s\n\n' "$section"
done

if [ -n "$previous" ]; then
  printf '**Full Changelog**: https://github.com/%s/compare/%s...%s\n' "$repository" "$previous" "$tag"
fi
