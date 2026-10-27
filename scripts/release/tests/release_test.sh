#!/usr/bin/env bash
set -euo pipefail

expected_tests=15
fixture_version="0.4.1"
fixture_rids=(osx-arm64 linux-x64)
linux_archive="jobranger-$fixture_version-linux-x64.tar.gz"
release_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
next_version="$release_dir/next_version.sh"
assemble="$release_dir/assemble.sh"

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

init_repo() {
  git init -q
}

commit() {
  git -c user.name=user -c user.email=user@example.com commit -q --allow-empty -m "$1"
}

tagged_repo() {
  init_repo
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
  mkdir stub
  cat >stub/uname <<'EOF'
#!/bin/sh
case "$1" in
  -s) echo "$STUB_UNAME_S" ;;
  -m) echo "$STUB_UNAME_M" ;;
esac
EOF
  chmod 0755 stub/uname
}

run_install() {
  STUB_UNAME_S="$1" STUB_UNAME_M="$2" \
    JOBRANGER_DOWNLOAD_URL="file://$PWD/out" \
    JOBRANGER_INSTALL_DIR="$PWD/bin-install" \
    PATH="$PWD/stub:$PATH" \
    sh out/install.sh
}

install_failure() {
  local status=0
  run_install "$1" "$2" >/dev/null 2>stderr.txt || status=$?
  assert_eq 1 "$status"
  cat stderr.txt
}

rewrite_checksums() {
  awk "$1" out/SHA256SUMS >SHA256SUMS.new
  mv SHA256SUMS.new out/SHA256SUMS
}

should_release_initial_version_when_no_cli_tag_exists() {
  init_repo
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
  for title in 'fix(CLI-4): fix CI' 'Bump ruff from 0.16.4 to 0.17.1 in /cli-python' 'Add user context' 'feature: x'; do
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
  (cd rerun && init_repo && commit 1 && git tag cli-v0.2.0)
  (cd older && init_repo && commit 1 && commit 2 && git tag cli-v0.3.0)

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

should_stamp_version_into_install_script() {
  build_release

  assert_contains "$(cat out/install.sh)" "version=\"$fixture_version\""
  assert_eq 0 "$(grep -c __JOBRANGER_VERSION__ out/install.sh)"
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
    output="$(run_install "$system" "$machine")"
    [ -x bin-install/jobranger ] || fail "bin-install/jobranger is not executable"
    assert_eq "$rid" "$(bin-install/jobranger)"
    assert_contains "$output" "jobranger $fixture_version installed to"
  done
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
