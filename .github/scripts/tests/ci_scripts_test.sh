#!/usr/bin/env bash
set -euo pipefail

expected_tests=9
scripts_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
missing_tests="$scripts_dir/missing_tests.sh"
macos_jobs="$scripts_dir/cli_macos_jobs.sh"
list_tests="$scripts_dir/tests/mocks/list-tests.txt"
catalogue_test="Jobranger.Cli.Tests.Catalogue.CatalogueTests.ShouldMatchRoleWord_WhenRoleIsSelected"
second_cv_test_line=8

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

compare_with() {
  bash "$missing_tests" "$list_tests" head.txt "$1"
}

without_catalogue_test() {
  sed "/$catalogue_test/d" "$list_tests" >head.txt
}

jobs_for() {
  printf '%s\n' "$@" | bash "$macos_jobs"
}

assert_jobs() {
  local expected actual
  expected="$(printf 'publish=%s\nsecrets=%s' "$2" "$3")"
  actual="$(jobs_for "$1")"
  [ "$expected" = "$actual" ] || fail "for $1" "expected: $expected" "actual:   $actual"
}

should_pass_when_head_keeps_every_base_test() {
  {
    cat "$list_tests"
    echo "  Jobranger.Cli.Tests.NewTests.ShouldRun_WhenAdded"
  } >head.txt

  assert_eq "kept all 5 tests from the base" "$(compare_with false)"
}

should_fail_when_base_test_is_missing() {
  without_catalogue_test
  local status=0

  compare_with false 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "missing: $catalogue_test"
}

should_fail_when_one_of_two_same_name_tests_is_missing() {
  sed "${second_cv_test_line}d" "$list_tests" >head.txt
  local status=0

  compare_with false 2>/dev/null || status=$?

  assert_eq 1 "$status"
}

should_pass_when_label_allows_removal() {
  without_catalogue_test

  local output
  output="$(compare_with true)" || fail "comparison failed with removal allowed"

  assert_contains "$output" "accepted 1 removed tests because the pull request has the tests-removed label"
}

should_fail_when_base_list_has_no_tests() {
  sed '/^  /d' "$list_tests" >base.txt
  cp "$list_tests" head.txt
  local status=0

  bash "$missing_tests" base.txt head.txt false 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "found no tests in"
}

should_skip_both_macos_jobs_when_every_file_is_on_skip_list() {
  local file
  for file in \
    dotnet/cli/README.md \
    dotnet/cli/src/Jobranger.Cli/NOTES.md \
    messages/fixtures/envelope/a.json \
    dotnet/cli/scripts/release/assemble.sh \
    dotnet/cli/deploy/Dockerfile \
    .github/actions/test-ratchet/action.yml \
    .github/scripts/missing_tests.sh; do
    assert_jobs "$file" false false
  done
}

should_run_one_macos_job_when_file_skips_the_other() {
  local row file publish secrets
  for row in \
    "dotnet/cli/tests/Jobranger.Cli.Tests/ConfigFileTests.cs false true" \
    "messages/fixtures/crypto/a.json false true" \
    "messages/fixtures/matching/a.json false true" \
    "dotnet/relay/src/Jobranger.Relay/Program.cs false true" \
    ".github/actions/cli-publish/action.yml true false"; do
    read -r file publish secrets <<<"$row"
    assert_jobs "$file" "$publish" "$secrets"
  done
}

should_run_both_macos_jobs_when_any_file_is_off_skip_list() {
  assert_eq "$(printf 'publish=true\nsecrets=true')" \
    "$(jobs_for dotnet/cli/README.md dotnet/cli/src/Jobranger.Cli/Program.cs dotnet/cli/deploy/Dockerfile)"
}

should_run_both_macos_jobs_when_path_is_unknown() {
  local file
  for file in \
    dotnet/cli/src/Jobranger.Cli/Program.cs \
    dotnet/.config/dotnet-tools.json \
    .gitattributes \
    dotnet/tools/New/New.csproj; do
    assert_jobs "$file" true true
  done
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
