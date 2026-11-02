#!/usr/bin/env bash
set -euo pipefail

expected_tests=46
fixture_version="0.4.1"
fixture_rids=(osx-arm64 linux-x64)
linux_archive="jobranger-$fixture_version-linux-x64.tar.gz"
release_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
next_version="$release_dir/next_version.sh"
assemble="$release_dir/assemble.sh"
release_prs="$release_dir/release_prs.sh"
check_prs="$release_dir/check_prs.sh"
check_main_head="$release_dir/check_main_head.sh"
pulls="$release_dir/tests/mocks/pulls.json"
check_runs="$release_dir/tests/mocks/check-runs.json"
workflow_runs="$release_dir/tests/mocks/workflow-runs.json"
ref_main="$release_dir/tests/mocks/ref-main.json"
repository="owner/jobranger"
head_sha="d97a9b2d93ad72a174cdcc9b1c2ecd7677ca1356"
backend_suite=112311523847
other_sha="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
unknown_sha="cccccccccccccccccccccccccccccccccccccccc"
endpoint_chars='/?=&'
mock_chars='____'
runs_endpoint="repos/$repository/actions/runs?head_sha=$head_sha&per_page=100"
checks_endpoint="repos/$repository/commits/$head_sha/check-runs?filter=latest&per_page=100"
main_endpoint="repos/$repository/git/ref/heads/main"
argument_limit_bytes=131072
check_copies=10

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

assert_missing() {
  [ ! -e "$1" ] || fail "expected no file at $1"
}

commit() {
  git -c user.name=user -c user.email=user@example.com commit -q --allow-empty -m "$1"
}

tagged_repo() {
  git init -q -b trunk
  commit 1
  local tag
  for tag in "$@"; do
    git tag "$tag"
  done
  commit 2
}

next_of() {
  bash "$next_version" HEAD "$1"
}

version_of() {
  next_of "$1" | sed -n 1p
}

assert_skipped() {
  assert_eq "$(printf 'version=\nprevious=')" "$(bash "$next_version" "$1" 'fix: x' 2>stderr.txt)"
  assert_contains "$(cat stderr.txt)" "is already in cli-v"
}

sha256_check() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum -c "$1"
  else
    shasum -a 256 -c "$1"
  fi
}

pack_archive() {
  local rid="$1"
  mkdir -p "pack-$rid" archives
  printf '#!/bin/sh\necho %s\n' "$rid" >"pack-$rid/jobranger"
  chmod 0755 "pack-$rid/jobranger"
  tar -czf "archives/jobranger-$fixture_version-$rid.tar.gz" -C "pack-$rid" jobranger
}

build_release() {
  local rid
  for rid in "${fixture_rids[@]}"; do
    pack_archive "$rid"
  done
  bash "$assemble" "$fixture_version" archives out
}

stub_uname() {
  mkdir -p stub
  cat >stub/uname <<'EOF'
#!/bin/sh
case "$1" in
  -s) echo "$STUB_UNAME_S" ;;
  -m) echo "$STUB_UNAME_M" ;;
esac
EOF
  cat >stub/sysctl <<'EOF'
#!/bin/sh
echo "${STUB_SYSCTL:-0}"
EOF
  chmod 0755 stub/uname stub/sysctl
}

stub_older() {
  mkdir -p "$1"
  printf '#!/bin/sh\necho older\n' >"$1/jobranger"
  chmod 0755 "$1/jobranger"
}

stub_curl() {
  mkdir -p stub
  cat >stub/curl <<EOF
#!/bin/sh
for url; do :; done
echo "\$url" >>"$PWD/curl.log"
exit 22
EOF
  chmod 0755 stub/curl
}

run_install() {
  stub_uname
  STUB_UNAME_S="$1" STUB_UNAME_M="$2" \
    JOBRANGER_INSTALL_DIR="$PWD/bin-install" \
    PATH="$PWD/stub:$PATH" \
    sh out/install.sh
}

run_local_install() {
  JOBRANGER_DOWNLOAD_URL="file://$PWD/out" run_install "$1" "$2"
}

install_failure() {
  local status=0
  run_local_install "$1" "$2" >/dev/null 2>stderr.txt || status=$?
  assert_eq 1 "$status"
  cat stderr.txt
}

rewrite_checksums() {
  awk "$1" out/SHA256SUMS >SHA256SUMS.new
  mv SHA256SUMS.new out/SHA256SUMS
}

stub_gh() {
  mkdir -p stub mocks
  cat >stub/gh <<EOF
#!/bin/sh
[ "\$1" = api ] || exit 1
mock="$PWD/mocks/\$(printf '%s' "\$2" | tr '$endpoint_chars' '$mock_chars').json"
if [ ! -f "\$mock" ]; then
  echo "no mock for \$2" >&2
  exit 1
fi
if [ "\$3" = --jq ]; then
  exec jq -r "\$4" "\$mock"
fi
exec cat "\$mock"
EOF
  chmod 0755 stub/gh
}

mock_file() {
  printf 'mocks/%s.json' "$(printf '%s' "$1" | tr "$endpoint_chars" "$mock_chars")"
}

mock_api() {
  cp "$2" "$(mock_file "$1")"
}

mock_edit() {
  jq "$3" "$2" >"$(mock_file "$1")"
}

with_stub() {
  PATH="$PWD/stub:$PATH" "$@"
}

pr_row() {
  printf '89\t%s\t%s\tfeat(CLI-13): add email delivery\n' "$head_sha" "$1"
}

gate_of() {
  printf '%s\n' "$1" | with_stub bash "$check_prs" "$repository"
}

green_mocks() {
  stub_gh
  mock_api "$runs_endpoint" "$workflow_runs"
  mock_api "$checks_endpoint" "$check_runs"
}

mock_checks() {
  mock_edit "$checks_endpoint" "$check_runs" "$1"
}

mock_failed_backend_check() {
  mock_checks "(.check_runs[] | select(.check_suite.id == $backend_suite)).conclusion = \"failure\""
}

add_test_cli_run() {
  mock_edit "$runs_endpoint" "$workflow_runs" ".workflow_runs += [.workflow_runs[] | select(.name == \"Test CLI\") | $1] | .total_count = 4"
}

add_failed_check() {
  mock_checks ".check_runs += [.check_runs[0] | $1 | .conclusion = \"failure\"] | .total_count = 9"
}

mock_pulls() {
  mock_edit "repos/$repository/commits/$1/pulls" "$pulls" "$2"
}

prs_since() {
  with_stub bash "$release_prs" "$repository" "$1" HEAD "$2"
}

should_pass_when_checks_are_success_skipped_or_neutral() {
  green_mocks

  local conclusion
  for conclusion in success skipped neutral; do
    mock_checks ".check_runs[0].conclusion = \"$conclusion\""
    gate_of "$(pr_row false)" >/dev/null 2>stderr.txt || fail "gate failed for $conclusion"
    assert_eq "" "$(cat stderr.txt)"
  done
}

should_name_pr_and_workflow_when_check_failed() {
  green_mocks
  mock_failed_backend_check
  local status=0

  gate_of "$(pr_row false)" 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "PR #89: Test backend / Format, build and test is failure"
}

should_fail_when_check_is_still_running() {
  green_mocks
  mock_checks '.check_runs[0].status = "in_progress" | .check_runs[0].conclusion = null'
  local status=0

  gate_of "$(pr_row false)" 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "is in_progress"
}

should_ignore_checks_of_older_run_of_same_workflow() {
  green_mocks
  add_test_cli_run '.id = 1 | .check_suite_id = 104 | .created_at = "2026-11-02T12:00:00Z"'
  add_failed_check '.check_suite.id = 104'

  gate_of "$(pr_row false)" >/dev/null || fail "gate failed on a check of an older run"
}

should_name_app_when_check_is_not_from_actions() {
  green_mocks
  add_failed_check '.name = "Code quality" | .check_suite.id = 900 | .app.slug = "other-app"'
  local status=0

  gate_of "$(pr_row false)" 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "PR #89: other-app / Code quality is failure"
}

should_skip_pr_with_waiver_label() {
  green_mocks
  mock_failed_backend_check

  local output
  output="$(gate_of "$(pr_row true)")" || fail "gate failed on a waived pull request"

  assert_contains "$output" "PR #89: checks waived by the release-checks-waived label"
}

should_fail_when_check_runs_are_not_json() {
  green_mocks
  echo "not json" >"$(mock_file "$checks_endpoint")"
  local status=0

  gate_of "$(pr_row false)" >/dev/null 2>&1 || status=$?

  [ "$status" -ne 0 ] || fail "gate passed on check runs that are not JSON"
}

should_gate_check_runs_larger_than_one_argument() {
  green_mocks
  mock_checks "[range($check_copies) as \$copy | .check_runs[]] as \$runs | .check_runs = \$runs | .total_count = (\$runs | length) | (.check_runs[] | select(.name == \"Generate, compare and test\")).conclusion = \"failure\""
  [ "$(wc -c <"$(mock_file "$checks_endpoint")")" -gt "$argument_limit_bytes" ] || fail "check runs mock is not over $argument_limit_bytes bytes"
  local status=0

  gate_of "$(pr_row false)" 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "PR #89: Check messages / Generate, compare and test is failure"
}

should_ignore_failed_dispatch_run_on_pr_head() {
  green_mocks
  add_test_cli_run '.id = 9 | .event = "workflow_dispatch" | .check_suite_id = 999 | .created_at = "2026-11-02T13:00:00Z"'
  add_failed_check '.check_suite.id = 999'

  gate_of "$(pr_row false)" >/dev/null || fail "gate failed on a check of a dispatch run"
}

should_fail_when_github_returns_part_of_a_list() {
  local row endpoint mock kind returned status
  for row in "$checks_endpoint|$check_runs|check runs|8" "$runs_endpoint|$workflow_runs|workflow runs|3"; do
    IFS='|' read -r endpoint mock kind returned <<<"$row"
    green_mocks
    mock_edit "$endpoint" "$mock" '.total_count = 120'
    status=0

    gate_of "$(pr_row false)" 2>stderr.txt || status=$?

    assert_eq 1 "$status"
    assert_contains "$(cat stderr.txt)" "PR #89: GitHub returned $returned of 120 $kind, and the gate reads at most 100"
  done
}

should_pass_when_pr_head_has_no_check_runs() {
  green_mocks
  mock_checks '.check_runs = [] | .total_count = 0'

  local output
  output="$(gate_of "$(pr_row false)")" || fail "gate failed on a pull request with no check runs"

  assert_contains "$output" "PR #89: no check runs found"
}

should_list_merged_pr_of_every_commit_since_previous_tag() {
  tagged_repo cli-v0.2.3
  commit 3
  stub_gh
  mock_pulls "$(git rev-parse HEAD~1)" .
  mock_pulls "$(git rev-parse HEAD)" '.[0].number = 90 | .[0].title = "fix: second"'

  local expected
  expected="$(printf '89\t%s\tfalse\tfeat(CLI-13): add email delivery\n90\t%s\tfalse\tfix: second' "$head_sha" "$head_sha")"

  assert_eq "$expected" "$(prs_since cli-v0.2.3 "")"
}

should_fail_when_commit_has_no_merged_pr() {
  tagged_repo cli-v0.2.3
  stub_gh
  mock_pulls "$(git rev-parse HEAD)" '.[0].merged_at = null'
  local status=0

  prs_since cli-v0.2.3 "" 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "commit $(git rev-parse HEAD) has no merged pull request"
}

should_mark_pr_waived_when_it_has_waiver_label() {
  tagged_repo cli-v0.2.3
  stub_gh
  mock_pulls "$(git rev-parse HEAD)" '.[0].labels = [{name: "release-checks-waived"}]'

  assert_eq true "$(prs_since cli-v0.2.3 "" | cut -f3)"
}

should_fail_when_previous_tag_is_unknown() {
  tagged_repo cli-v0.2.3
  stub_gh
  mock_pulls "$(git rev-parse HEAD)" .
  local output status=0

  output="$(prs_since cli-v9.9.9 "" 2>/dev/null)" || status=$?

  [ "$status" -ne 0 ] || fail "release_prs.sh passed with an unknown tag"
  assert_eq "" "$output"
}

should_print_nothing_when_range_is_empty() {
  tagged_repo cli-v0.2.3
  stub_gh

  local output
  output="$(with_stub bash "$release_prs" "$repository" cli-v0.2.3 HEAD~1 "")" || fail "release_prs.sh failed on an empty range"

  assert_eq "" "$output"
}

should_print_usage_when_argument_count_is_wrong() {
  local status=0

  bash "$release_prs" "$repository" cli-v0.2.3 HEAD 2>stderr.txt || status=$?

  assert_eq 2 "$status"
  assert_eq "usage: release_prs.sh <repository> <previous-tag> <commit> <accepted-commits>" "$(cat stderr.txt)"
}

should_accept_listed_commit_without_pr() {
  tagged_repo cli-v0.2.3
  stub_gh
  local sha output
  sha="$(git rev-parse HEAD)"
  mock_pulls "$sha" '.[0].merged_at = null'

  output="$(prs_since cli-v0.2.3 "$sha" 2>stderr.txt)" || fail "release_prs.sh failed on an accepted commit"

  assert_eq "" "$output"
  assert_contains "$(cat stderr.txt)" "commit $sha accepted without a pull request by the run input"
}

should_fail_when_accepted_commit_is_not_in_range() {
  tagged_repo cli-v0.2.3
  stub_gh
  mock_pulls "$(git rev-parse HEAD)" .
  local status=0

  prs_since cli-v0.2.3 "$unknown_sha" >/dev/null 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "accepted commit $unknown_sha is not in cli-v0.2.3..HEAD"
}

should_bump_minor_when_any_title_is_feat() {
  tagged_repo cli-v0.2.3

  assert_eq "version=0.3.0" "$(version_of "$(printf 'fix: a\nfeat(CLI-1): b\nfix: c')")"
}

should_bump_major_when_any_title_is_breaking_from_1_0() {
  tagged_repo cli-v1.4.2

  assert_eq "version=2.0.0" "$(version_of "$(printf 'feat: a\nrefactor(CLI-2)!: b\nfeat: c')")"
}

head_check_of() {
  with_stub bash "$check_main_head" "$repository" "$1"
}

main_mock() {
  stub_gh
  mock_api "$main_endpoint" "$ref_main"
}

should_fail_when_main_moved_during_run() {
  main_mock
  local main_sha status=0
  main_sha="$(jq -r .object.sha "$ref_main")"

  head_check_of "$other_sha" 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "main is at $main_sha, and this run releases $other_sha"
}

should_pass_when_main_is_run_commit() {
  main_mock

  head_check_of "$(jq -r .object.sha "$ref_main")" || fail "head check failed when main is the run commit"
}

should_fail_when_main_head_lookup_fails() {
  stub_gh
  local status=0

  head_check_of "$other_sha" 2>stderr.txt || status=$?

  [ "$status" -ne 0 ] || fail "head check passed when the main lookup failed"
  case "$(cat stderr.txt)" in
    *"main is at"*) fail "head check compared an empty main head" ;;
  esac
}

should_release_initial_version_when_no_cli_tag_exists() {
  git init -q -b trunk
  commit 1

  assert_eq "$(printf 'version=0.1.0\nprevious=')" "$(next_of 'fix: x')"
}

should_bump_minor_for_feat_title() {
  tagged_repo cli-v0.2.3

  assert_eq "$(printf 'version=0.3.0\nprevious=cli-v0.2.3')" "$(next_of 'feat(CLI-1): add run fetch')"
}

should_bump_patch_for_other_titles() {
  tagged_repo cli-v0.2.3

  local title
  for title in 'fix(CLI-4): fix CI' 'feature: x'; do
    assert_eq "version=0.2.4" "$(version_of "$title")"
  done
}

should_bump_minor_for_breaking_title_before_1_0() {
  tagged_repo cli-v0.2.3

  assert_eq "version=0.3.0" "$(version_of 'fix!: drop pip')"
}

should_bump_major_for_breaking_title_from_1_0() {
  tagged_repo cli-v1.4.2

  assert_eq "version=2.0.0" "$(version_of 'refactor(CLI-2)!: rename flags')"
}

should_skip_commit_already_in_a_release() {
  mkdir rerun older
  (cd rerun && git init -q -b trunk && commit 1 && git tag cli-v0.2.0)
  (cd older && git init -q -b trunk && commit 1 && commit 2 && git tag cli-v0.3.0)

  (cd rerun && assert_skipped HEAD)
  (cd older && assert_skipped HEAD~1)
}

should_ignore_tags_outside_cli_version_pattern() {
  tagged_repo cli-v0.2.0 cli-v1.0.0-rc1 web-v9.0.0 v5.0.0

  assert_eq "$(printf 'version=0.2.1\nprevious=cli-v0.2.0')" "$(next_of 'fix: x')"
}

should_pick_highest_version_numerically() {
  tagged_repo cli-v0.9.0 cli-v0.10.0

  assert_eq "version=0.10.1" "$(version_of 'fix: x')"
}

should_download_from_release_tag_when_no_url_is_set() {
  build_release
  stub_curl

  run_install Linux x86_64 2>/dev/null || true

  assert_contains "$(cat curl.log)" "/releases/download/cli-v$fixture_version/$linux_archive"
}

should_list_every_asset_in_checksums() {
  build_release

  assert_eq 3 "$(wc -l <out/SHA256SUMS | tr -d ' ')"
  (cd out && sha256_check SHA256SUMS >/dev/null)
}

should_fail_when_archive_is_missing() {
  pack_archive linux-x64
  local status=0

  bash "$assemble" "$fixture_version" archives out 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "missing archive jobranger-$fixture_version-osx-arm64.tar.gz"
}

should_install_build_for_platform() {
  build_release

  local row system machine rid output
  for row in "Linux x86_64 linux-x64" "Darwin arm64 osx-arm64"; do
    read -r system machine rid <<<"$row"
    output="$(run_local_install "$system" "$machine")"
    [ -x bin-install/jobranger ] || fail "bin-install/jobranger is not executable"
    assert_eq "$rid" "$(bin-install/jobranger)"
    assert_contains "$output" "jobranger $fixture_version installed to"
  done
}

should_install_arm64_build_under_rosetta() {
  build_release

  STUB_SYSCTL=1 run_local_install Darwin x86_64 >/dev/null

  assert_eq osx-arm64 "$(bin-install/jobranger)"
}

should_warn_when_other_jobranger_runs_first() {
  build_release
  stub_older older
  local output

  output="$(PATH="$PWD/older:$PATH" run_local_install Linux x86_64)"

  assert_contains "$output" "$PWD/older/jobranger runs first on PATH, remove it or install again with JOBRANGER_INSTALL_DIR=\"$PWD/older\""
}

should_quote_install_dir_in_hint_when_path_has_space() {
  build_release
  stub_older "old er"
  local output

  output="$(PATH="$PWD/old er:$PATH" run_local_install Linux x86_64)"

  assert_contains "$output" "JOBRANGER_INSTALL_DIR=\"$PWD/old er\""
}

should_not_warn_when_install_dir_runs_first() {
  build_release
  stub_older older
  local output

  output="$(PATH="$PWD/bin-install:$PWD/older:$PATH" run_local_install Linux x86_64)"

  assert_eq "jobranger $fixture_version installed to $PWD/bin-install/jobranger" "$output"
}

should_ask_to_add_install_dir_when_not_on_path() {
  build_release

  assert_contains "$(run_local_install Linux x86_64)" "add $PWD/bin-install to PATH to run jobranger"
}

should_not_ask_to_add_install_dir_when_path_has_trailing_slash() {
  build_release
  local output

  output="$(PATH="$PWD/bin-install/:$PATH" run_local_install Linux x86_64)"

  assert_eq "jobranger $fixture_version installed to $PWD/bin-install/jobranger" "$output"
}

should_refuse_when_no_checksum_tool_exists() {
  build_release
  mkdir -p tools
  ln -s "$(command -v curl)" tools/curl
  local shell status=0
  shell="$(command -v sh)"

  PATH="$PWD/tools" JOBRANGER_INSTALL_DIR="$PWD/bin-install" "$shell" out/install.sh 2>stderr.txt || status=$?

  assert_eq 1 "$status"
  assert_contains "$(cat stderr.txt)" "no checksum tool found"
  assert_missing bin-install/jobranger
}

should_refuse_archive_when_checksum_differs() {
  build_release
  rewrite_checksums "\$2 == \"$linux_archive\" { \$1 = \"$(printf '%064d' 0)\" } { print }"

  assert_contains "$(install_failure Linux x86_64)" "checksum mismatch for $linux_archive"
  assert_missing bin-install/jobranger
}

should_refuse_when_checksums_lack_archive() {
  build_release
  rewrite_checksums "\$2 != \"$linux_archive\""

  assert_contains "$(install_failure Linux x86_64)" "SHA256SUMS has no entry for $linux_archive"
  assert_missing bin-install/jobranger
}

should_refuse_platform_without_build() {
  build_release

  local platform system machine
  for platform in "Linux aarch64" "Darwin x86_64"; do
    read -r system machine <<<"$platform"
    assert_contains "$(install_failure "$system" "$machine")" "no build for $platform"
  done
  assert_missing bin-install/jobranger
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
