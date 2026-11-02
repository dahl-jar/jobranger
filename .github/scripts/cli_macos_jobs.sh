#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 0 ]; then
  echo "usage: cli_macos_jobs.sh" >&2
  exit 2
fi

publish=false
secrets=false
while IFS= read -r file; do
  [ -n "$file" ] || continue
  case "$file" in
    *.md | dotnet/cli/scripts/release/* | dotnet/cli/deploy/* \
      | .github/actions/test-ratchet/* | .github/scripts/missing_tests.sh)
      ;;
    messages/fixtures/crypto/* | messages/fixtures/matching/*)
      secrets=true
      ;;
    messages/fixtures/*)
      ;;
    dotnet/cli/tests/* | dotnet/messages/tests/* | dotnet/relay/*)
      secrets=true
      ;;
    .github/actions/cli-publish/*)
      publish=true
      ;;
    *)
      publish=true
      secrets=true
      ;;
  esac
done
printf 'publish=%s\nsecrets=%s\n' "$publish" "$secrets"
