#!/usr/bin/env bash

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

run_tests() {
  local expected_tests="$1" name dir status passed=0 failed=0
  local tests=()
  test_root="$(mktemp -d)"
  trap 'rm -rf "$test_root"' EXIT

  while read -r name; do
    tests+=("$name")
  done < <(declare -F | awk '$3 ~ /^should_/ { print $3 }')

  for name in "${tests[@]}"; do
    dir="$test_root/$name"
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
}
