#!/usr/bin/env bash
set -euo pipefail

source_image="ghcr.io/dahl-jar/jobranger-relay"
target_image="ghcr.io/dahl-jar/jobranger/relay"
pin_dir="versions/jobranger-relay"
pin_file="$pin_dir/kustomization.yaml"
version_file="deploy/jobranger-relay/version.yaml"
version_writer="scripts/write_version_file.py"
service_name="jobranger-relay"
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

write_and_commit() {
  committed=false
  (
    cd "$pin_dir"
    kustomize edit set image "$source_image=$target_image:sha-$commit@$digest"
  )
  local files=("$pin_file")
  if [ -f "$version_file" ]; then
    python3 "$version_writer" "$version_file" --name "$service_name" --version "sha-$commit" --digest "$digest"
    files+=("$version_file")
  else
    echo "version file $version_file not in homelab@prod, skipping"
  fi
  if git diff --quiet -- "${files[@]}"; then
    echo "image already pinned to sha-$commit, nothing to do"
    return
  fi
  git config user.name jobranger-ci
  git config user.email jobranger-ci@users.noreply.github.com
  git commit -m "Deploy jobranger-relay sha-$commit" -- "${files[@]}"
  committed=true
}

attempt=1
while true; do
  write_and_commit
  if [ "$committed" = false ]; then
    exit 0
  fi
  if git push; then
    exit 0
  fi
  if [ "$attempt" -eq "$max_push_attempts" ]; then
    echo "push failed after $max_push_attempts attempts" >&2
    exit 1
  fi
  echo "push attempt $attempt of $max_push_attempts failed, retrying in $retry_delay_seconds seconds" >&2
  sleep "$retry_delay_seconds"
  git fetch origin
  git reset --hard "@{upstream}"
  attempt=$((attempt + 1))
done
