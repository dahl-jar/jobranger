#!/usr/bin/env bash
set -euo pipefail

expected_tests=35
fixture_version="0.4.1"
fixture_rids=(osx-arm64 linux-x64)
linux_archive="jobranger-$fixture_version-linux-x64.tar.gz"
release_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
next_version="$release_dir/next_version.sh"
assemble="$release_dir/assemble.sh"
release_title="$release_dir/release_title.sh"
release_notes="$release_dir/release_notes.sh"
pulls="$release_dir/tests/mocks/pulls.json"
repository="owner/jobranger"
compare_link="**Full Changelog**: https://github.com/$repository/compare/cli-v0.2.3...cli-v0.2.4"

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

released_repo() {
  git init -q -b trunk
  commit released
  git tag cli-v0.2.3
  echo '[]' >none.json
  stub_gh "$PWD/none.json"
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
  mkdir -p stub
  cat >stub/gh <<EOF
#!/bin/sh
case "\$1 \$2 \$3" in
  "api repos/$repository/commits/"*"/pulls --jq") ;;
  *) exit 1 ;;
esac
commit="\${2#repos/$repository/commits/}"
commit="\${commit%/pulls}"
response="$PWD/responses/\$commit.json"
[ -f "\$response" ] || response="$1"
exec jq -r "\$4" "\$response"
EOF
  chmod 0755 stub/gh
}

title_of() {
  PATH="$PWD/stub:$PATH" bash "$release_title" "$repository" "$1" "$2"
}

pr_body() {
  printf '## What does this PR do?\n\n%s\n\n## Known risks\n\nNone.\n' "$1"
}

pr_commit() {
  commit "commit $1"
  mkdir -p responses
  jq --argjson number "$1" --arg title "$2" --arg body "$3" \
    '.[1] += {number: $number, title: $title, body: $body}' "$pulls" \
    >"responses/$(git rev-parse HEAD).json"
}

notes_of() {
  PATH="$PWD/stub:$PATH" bash "$release_notes" "$repository" HEAD cli-v0.2.4 "$1" 2>stderr.txt
}

should_use_pr_title_when_set() {
  stub_gh "$pulls"

  assert_eq "fix: pr title" "$(title_of HEAD 'fix: pr title')"
}

should_use_merged_pr_title_when_pr_title_empty() {
  stub_gh "$pulls"

  assert_eq "fix: merged change" "$(title_of HEAD '')"
}

should_use_commit_subject_when_no_merged_pr() {
  git init -q -b trunk
  commit 'feat: commit subject'
  echo '[]' >none.json
  stub_gh "$PWD/none.json"

  assert_eq "feat: commit subject" "$(title_of HEAD '')"
}

should_note_only_what_does_this_pr_do_section() {
  tagged_repo cli-v0.2.3
  stub_gh "$pulls"

  assert_eq "$(printf -- '- Adds run fetch.\n- Prints a digest.\n\n%s' "$compare_link")" "$(notes_of cli-v0.2.3)"
}

should_note_every_pr_since_previous_tag() {
  released_repo
  pr_commit 9 'fix: one' "$(pr_body '- One.')"
  pr_commit 10 'fix: two' "$(pr_body '- Two.')"

  assert_eq "$(printf -- '- One.\n\n- Two.\n\n%s' "$compare_link")" "$(notes_of cli-v0.2.3)"
}

should_note_pr_once_when_commits_share_it() {
  released_repo
  pr_commit 9 'fix: one' "$(pr_body '- One.')"
  pr_commit 9 'fix: one' "$(pr_body '- One.')"

  assert_eq "$(printf -- '- One.\n\n%s' "$compare_link")" "$(notes_of cli-v0.2.3)"
}

should_note_pr_title_when_section_has_no_text() {
  released_repo
  pr_commit 9 'fix: no heading' 'Closes #1'
  pr_commit 10 'fix: blank section' $'## What does this PR do?\n \n\n## Known risks\n\nNone.'

  assert_eq "$(printf -- '- fix: no heading\n\n- fix: blank section\n\n%s' "$compare_link")" "$(notes_of cli-v0.2.3)"
  assert_contains "$(cat stderr.txt)" "PR 9 has no text under ## What does this PR do?"
}

should_note_commit_subject_when_no_merged_pr() {
  released_repo
  commit 'feat: commit subject'

  assert_eq "$(printf -- '- feat: commit subject\n\n%s' "$compare_link")" "$(notes_of cli-v0.2.3)"
  assert_contains "$(cat stderr.txt)" "no merged PR for commit"
}

should_keep_code_block_when_it_contains_heading() {
  released_repo
  local section
  section="$(printf '%s\n' '- Adds a flag.' '' '```text' '## not a heading' '  ' "\$(touch injected)" '```' '' '- Keeps output.')"
  pr_commit 9 'fix: title' "$(pr_body "$section")"

  assert_eq "$(printf '%s\n\n%s' "$section" "$compare_link")" "$(notes_of cli-v0.2.3)"
  assert_missing injected
}

should_find_section_in_body_with_crlf_line_endings() {
  released_repo
  pr_commit 9 'fix: title' $'## What does this PR do?\r\n\r\n- Adds a flag.\r\n\r\n## Known risks\r\n\r\nNone.\r\n'

  assert_eq "$(printf -- '- Adds a flag.\n\n%s' "$compare_link")" "$(notes_of cli-v0.2.3)"
}

should_omit_compare_link_on_first_release() {
  git init -q -b trunk
  commit 'feat: first'
  echo '[]' >none.json
  stub_gh "$PWD/none.json"

  assert_eq "- feat: first" "$(notes_of '')"
}

should_fail_notes_when_pr_lookup_fails() {
  tagged_repo cli-v0.2.3
  stub_gh "$PWD/missing.json"
  local status=0

  notes_of cli-v0.2.3 >/dev/null || status=$?

  [ "$status" -ne 0 ] || fail "expected notes to fail"
}

should_fail_notes_when_previous_tag_is_unknown() {
  git init -q -b trunk
  commit 1
  echo '[]' >none.json
  stub_gh "$PWD/none.json"
  local status=0

  notes_of cli-v0.0.0 >/dev/null || status=$?

  [ "$status" -ne 0 ] || fail "expected notes to fail"
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
