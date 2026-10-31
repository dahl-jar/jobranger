#!/usr/bin/env bash
set -euo pipefail

expected_tests=11
release_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pin_image="$release_dir/pin_image.sh"
workflow="$release_dir/../../../../.github/workflows/release-relay.yml"
commit=1111111111111111111111111111111111111111
digest=sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
pin_dir=versions/jobranger-relay
pin_file="$pin_dir/kustomization.yaml"

while read -r variable; do
  unset "$variable"
done < <(git rev-parse --local-env-vars)
export GIT_CONFIG_GLOBAL=/dev/null
export GIT_CONFIG_NOSYSTEM=1

fail() {
  printf '%s\n' "$@" >&2
  exit 1
}

assert_eq() {
  [ "$1" = "$2" ] || fail "expected: $1" "actual:   $2"
}

assert_contains() {
  case "$1" in
    *"$2"*) ;;
    *) fail "expected to contain: $2" "actual: $1" ;;
  esac
}

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
  chmod 0755 stub/kustomize stub/sleep
}

stub_git() {
  mkdir -p stub
  cat >stub/git <<'EOF'
#!/bin/sh
echo "git $*" >>"$STUB_LOG"
case "$1" in
  diff) exit "${STUB_DIFF_EXIT:-1}" ;;
  pull) exit "${STUB_PULL_EXIT:-0}" ;;
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
  run_stubbed "$commit" "$digest" >/dev/null

  assert_contains "$(cat calls.log)" "git commit -m Deploy jobranger-relay sha-$commit -- $pin_file"
}

should_skip_commit_when_pin_unchanged() {
  local output

  output="$(STUB_DIFF_EXIT=0 run_stubbed "$commit" "$digest")"

  assert_eq "image already pinned to sha-$commit, nothing to do" "$output"
  assert_lacks "$(cat calls.log)" "git commit"
  assert_lacks "$(cat calls.log)" "git pull"
  assert_lacks "$(cat calls.log)" "git push"
}

should_pull_before_push() {
  run_stubbed "$commit" "$digest" >/dev/null

  assert_eq "$(printf 'git pull --rebase\ngit push')" "$(grep -E '^git (pull|push)' calls.log)"
}

should_retry_push_after_race() {
  STUB_PUSH_FAILS=1 run_stubbed "$commit" "$digest" >/dev/null 2>&1

  assert_eq 2 "$(count_of 'git pull')"
  assert_eq 2 "$(count_of 'git push')"
  assert_eq 1 "$(count_of 'sleep 5')"
}

should_fail_after_three_rejected_pushes() {
  local status=0

  STUB_PUSH_FAILS=99 run_stubbed "$commit" "$digest" >/dev/null 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_eq 3 "$(count_of 'git push')"
  assert_contains "$(cat stderr.txt)" "push failed after 3 attempts"
}

should_stop_when_rebase_fails() {
  local status=0

  STUB_PULL_EXIT=1 run_stubbed "$commit" "$digest" >/dev/null 2>&1 || status=$?

  [ "$status" -ne 0 ] || fail "expected a non-zero exit"
  assert_lacks "$(cat calls.log)" "git push"
}

should_refuse_bad_digest() {
  local bad status

  for bad in "" "sha256:abc"; do
    status=0
    run_stubbed "$commit" "$bad" 2>stderr.txt || status=$?
    assert_eq 2 "$status"
    assert_contains "$(cat stderr.txt)" "digest must match sha256:<64 hex>, got '$bad'"
  done

  assert_lacks "$(cat calls.log)" "edit set image"
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
  seed_push first
  git clone -q --depth 1 -b trunk "file://$PWD/origin.git" manifests
  seed_push moved

  (cd manifests && stub_tools && run_pin "$commit" "$digest" >/dev/null 2>&1)

  assert_eq "$(printf 'jobranger-ci Deploy jobranger-relay sha-%s\nuser moved' "$commit")" \
    "$(git -C origin.git log --format='%an %s' -2 trunk)"
}

root="$(mktemp -d)"
trap 'rm -rf "$root"' EXIT

tests=()
while read -r name; do
  tests+=("$name")
done < <(declare -F | awk '$3 ~ /^should_/ { print $3 }')

passed=0
failed=0
for name in "${tests[@]}"; do
  dir="$root/$name"
  mkdir "$dir"
  set +e
  (
    set -e
    cd "$dir"
    "$name"
  )
  status=$?
  set -e
  if [ "$status" -eq 0 ]; then
    passed=$((passed + 1))
    printf 'ok   %s\n' "$name"
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n' "$name"
  fi
done

printf '%d passed, %d failed\n' "$passed" "$failed"
[ "${#tests[@]}" -eq "$expected_tests" ] || fail "found ${#tests[@]} tests, expected $expected_tests"
[ "$failed" -eq 0 ]
