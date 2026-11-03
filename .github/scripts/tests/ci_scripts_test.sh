#!/usr/bin/env bash
set -euo pipefail

expected_tests=5
scripts_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
missing_tests="$scripts_dir/missing_tests.sh"
list_tests="$scripts_dir/tests/mocks/list-tests.txt"
catalogue_test="Jobranger.Cli.Tests.Catalogue.CatalogueTests.ShouldMatchRoleWord_WhenRoleIsSelected"
second_cv_test_line=8

source "$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)/.github/scripts/tests/shell_test_runner.sh"

compare_with() {
  bash "$missing_tests" "$list_tests" head.txt "$1"
}

without_catalogue_test() {
  sed "/$catalogue_test/d" "$list_tests" >head.txt
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

run_tests "$expected_tests"
