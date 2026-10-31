#!/usr/bin/env bash
set -euo pipefail

source_image="ghcr.io/dahl-jar/jobranger-relay"
target_image="ghcr.io/dahl-jar/jobranger/relay"
pin_dir="versions/jobranger-relay"
pin_file="$pin_dir/kustomization.yaml"
digest_pattern='^sha256:[0-9a-f]{64}$'
max_push_attempts=3
retry_delay_seconds=5

if [ "$#" -ne 2 ]; then
  echo "usage: pin_image.sh <commit> <digest>" >&2
  exit 2
fi
commit="$1"
digest="$2"

if [[ ! "$digest" =~ $digest_pattern ]]; then
  echo "digest must match sha256:<64 hex>, got '$digest'" >&2
  exit 2
fi

(
  cd "$pin_dir"
  kustomize edit set image "$source_image=$target_image:sha-$commit@$digest"
)

if git diff --quiet -- "$pin_file"; then
  echo "image already pinned to sha-$commit, nothing to do"
  exit 0
fi

git config user.name jobranger-ci
git config user.email jobranger-ci@users.noreply.github.com
git commit -m "Deploy jobranger-relay sha-$commit" -- "$pin_file"

attempt=1
while true; do
  git pull --rebase
  if git push; then
    exit 0
  fi
  if [ "$attempt" -eq "$max_push_attempts" ]; then
    echo "push failed after $max_push_attempts attempts" >&2
    exit 1
  fi
  echo "push attempt $attempt of $max_push_attempts failed, retrying in $retry_delay_seconds seconds" >&2
  sleep "$retry_delay_seconds"
  attempt=$((attempt + 1))
done
