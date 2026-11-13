#!/usr/bin/env bash
set -euo pipefail

expected_tests=13
release_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pin_image="$release_dir/pin_image.sh"
workflow="$release_dir/../../../../.github/workflows/release-relay.yml"
commit=1111111111111111111111111111111111111111
digest=sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
pin_dir=versions/jobranger-relay
pin_file="$pin_dir/kustomization.yaml"
version_file=deploy/jobranger-relay/version.yaml
version_writer=scripts/write_version_file.py
service_name=jobranger-relay
relay_version=sha-8a624cf87281f07420760b30785b6d60db8f6628
relay_digest=sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc
other_commit=2222222222222222222222222222222222222222
other_digest=sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
writer_call="python3 $version_writer $version_file --name $service_name --version sha-$commit --digest $digest"

while read -r variable; do
  unset "$variable"
done < <(git rev-parse --local-env-vars)
export GIT_CONFIG_GLOBAL=/dev/null
export GIT_CONFIG_NOSYSTEM=1

source "$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)/.github/scripts/tests/shell_test_runner.sh"

assert_lacks() {
  case "$1" in
    *"$2"*) fail "expected to lack: $2" "actual: $1" ;;
  esac
}

count_of() {
  grep -c -- "$1" calls.log || true
}

stub_tools() {
  mkdir -p stub
  cat >stub/kustomize <<'EOF'
#!/bin/sh
echo "$PWD $*" >>"$STUB_LOG"
echo "$*" >>kustomization.yaml
exit "${STUB_KUSTOMIZE_EXIT:-0}"
EOF
  cat >stub/sleep <<'EOF'
#!/bin/sh
echo "sleep $*" >>"$STUB_LOG"
EOF
  cat >stub/python3 <<'EOF'
#!/bin/sh
echo "python3 $*" >>"$STUB_LOG"
[ "${STUB_WRITER_EXIT:-0}" -eq 0 ] || exit "$STUB_WRITER_EXIT"
file="$2"
shift 2
while [ "$#" -gt 0 ]; do
  case "$1" in
    --version) version="$2" ;;
    --digest) digest="$2" ;;
  esac
  shift 2
done
sed -i.bak -e "s|^version: .*|version: \"$version\"|" -e "s|^digest: .*|digest: $digest|" "$file"
rm "$file.bak"
EOF
  chmod 0755 stub/kustomize stub/sleep stub/python3
}

stub_git() {
  mkdir -p stub
  cat >stub/git <<'EOF'
#!/bin/sh
echo "git $*" >>"$STUB_LOG"
case "$1" in
  diff) exit "${STUB_DIFF_EXIT:-1}" ;;
  fetch) exit "${STUB_FETCH_EXIT:-0}" ;;
  push)
    pushes="$(cat "$STUB_LOG.pushes" 2>/dev/null || echo 0)"
    echo "$((pushes + 1))" >"$STUB_LOG.pushes"
    [ "$pushes" -ge "${STUB_PUSH_FAILS:-0}" ]
    ;;
esac
EOF
  chmod 0755 stub/git
}

run_pin() {
  mkdir -p "$pin_dir"
  touch calls.log
  PATH="$PWD/stub:$PATH" STUB_LOG="$PWD/calls.log" bash "$pin_image" "$@"
}

run_stubbed() {
  stub_tools
  stub_git
  run_pin "$@"
}

write_version_sample() {
  mkdir -p "$1/deploy/jobranger-relay"
  cat >"$1/$version_file" <<EOF
apiVersion: homelab.dev/v1
kind: Version
metadata:
  name: $service_name
  annotations:
    config.kubernetes.io/local-config: "true"
version: "$2"
digest: $3
EOF
}

seed_version_file() {
  write_version_sample . "$relay_version" "$relay_digest"
}

seed_push() {
  git -C seed add .
  git -C seed -c user.name=user -c user.email=user@example.com commit -q --allow-empty -m "$1"
  git -C seed push -q origin trunk
}

should_pin_tag_and_digest_in_versions_folder() {
  local repository
  repository="$(sed -n 's/.*ContainerRepository=\([^ ]*\).*/\1/p' "$workflow")"

  run_stubbed "$commit" "$digest" >/dev/null

  assert_contains "$(cat calls.log)" "$PWD/$pin_dir edit set image ghcr.io/${repository%%/*}/jobranger-relay=ghcr.io/$repository:sha-$commit@$digest"
}

should_commit_with_deploy_subject() {
  seed_version_file

  run_stubbed "$commit" "$digest" >/dev/null

  assert_contains "$(cat calls.log)" "git commit -m Deploy jobranger-relay sha-$commit -- $pin_file $version_file"
}

should_write_version_file_when_it_exists() {
  seed_version_file

  run_stubbed "$commit" "$digest" >/dev/null

  assert_contains "$(cat calls.log)" "$writer_call"
}

should_skip_version_file_when_missing() {
  local output

  output="$(run_stubbed "$commit" "$digest")"

  assert_contains "$output" "version file $version_file not in homelab@prod, skipping"
  assert_lacks "$(cat calls.log)" "python3"
  assert_contains "$(cat calls.log)" "git commit -m Deploy jobranger-relay sha-$commit -- $pin_file"
  assert_lacks "$(cat calls.log)" "$pin_file $version_file"
}

should_stop_when_version_file_write_fails() {
  local status=0

  seed_version_file

  STUB_WRITER_EXIT=7 run_stubbed "$commit" "$digest" >/dev/null 2>&1 || status=$?

  assert_eq 7 "$status"
  assert_lacks "$(cat calls.log)" "git commit"
}

should_skip_commit_when_pin_unchanged() {
  local output

  seed_version_file

  output="$(STUB_DIFF_EXIT=0 run_stubbed "$commit" "$digest")"

  assert_eq "image already pinned to sha-$commit, nothing to do" "$output"
  assert_lacks "$(cat calls.log)" "git commit"
  assert_lacks "$(cat calls.log)" "git fetch"
  assert_lacks "$(cat calls.log)" "git push"
}

should_retry_push_after_race() {
  local commit_call expected

  seed_version_file
  commit_call="git commit -m Deploy jobranger-relay sha-$commit -- $pin_file $version_file"
  expected="$(printf '%s\n' "$writer_call" "$commit_call" "git push" "git fetch origin" "git reset --hard @{upstream}" "$writer_call" "$commit_call" "git push")"

  STUB_PUSH_FAILS=1 run_stubbed "$commit" "$digest" >/dev/null 2>&1

  assert_eq "$expected" "$(grep -E '^(python3|git (commit|push|fetch|reset))' calls.log)"
  assert_eq 1 "$(count_of 'sleep 5')"
}

should_fail_after_three_rejected_pushes() {
  local status=0

  STUB_PUSH_FAILS=99 run_stubbed "$commit" "$digest" >/dev/null 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_eq 3 "$(count_of 'git push')"
  assert_contains "$(cat stderr.txt)" "push failed after 3 attempts"
}

should_stop_when_fetch_fails() {
  local status=0

  STUB_PUSH_FAILS=1 STUB_FETCH_EXIT=1 run_stubbed "$commit" "$digest" >/dev/null 2>&1 || status=$?

  [ "$status" -ne 0 ] || fail "expected a non-zero exit"
  assert_eq 1 "$(count_of 'git push')"
  assert_lacks "$(cat calls.log)" "git reset"
}

should_refuse_bad_digest() {
  local bad status

  seed_version_file

  for bad in "" "sha256:abc"; do
    status=0
    run_stubbed "$commit" "$bad" 2>stderr.txt || status=$?
    assert_eq 2 "$status"
    assert_contains "$(cat stderr.txt)" "digest must match sha256:<64 hex>, got '$bad'"
  done

  assert_lacks "$(cat calls.log)" "edit set image"
  assert_lacks "$(cat calls.log)" "python3"
}

should_stop_when_kustomize_fails() {
  local status=0

  STUB_KUSTOMIZE_EXIT=1 run_stubbed "$commit" "$digest" >/dev/null 2>&1 || status=$?

  [ "$status" -ne 0 ] || fail "expected a non-zero exit"
  assert_lacks "$(cat calls.log)" "git commit"
}

should_print_usage_without_digest_argument() {
  local status=0

  run_stubbed "$commit" 2>stderr.txt || status=$?

  assert_eq 2 "$status"
  assert_contains "$(cat stderr.txt)" "usage: pin_image.sh <commit> <digest>"
}

should_land_pin_on_moved_trunk_with_real_git() {
  git init -q --bare -b trunk origin.git
  git init -q -b trunk seed
  git -C seed remote add origin "$PWD/origin.git"
  mkdir -p "seed/$pin_dir"
  touch "seed/$pin_file"
  write_version_sample seed "$relay_version" "$relay_digest"
  seed_push first
  git clone -q --depth 1 -b trunk "file://$PWD/origin.git" manifests
  write_version_sample seed "sha-$other_commit" "$other_digest"
  seed_push moved

  (cd manifests && stub_tools && run_pin "$commit" "$digest" >/dev/null 2>&1)

  assert_eq "$(printf 'jobranger-ci Deploy jobranger-relay sha-%s\nuser moved' "$commit")" \
    "$(git -C origin.git log --format='%an %s' -2 trunk)"
  assert_contains "$(git -C origin.git show "trunk:$version_file")" "version: \"sha-$commit\""
  assert_contains "$(git -C origin.git show "trunk:$version_file")" "digest: $digest"
}

run_tests "$expected_tests"
