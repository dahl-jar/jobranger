#!/bin/sh
set -eu

version="__JOBRANGER_VERSION__"
release_url="https://github.com/dahl-jar/jobranger/releases/download/cli-v$version"
download_url="${JOBRANGER_DOWNLOAD_URL:-$release_url}"
install_dir="${JOBRANGER_INSTALL_DIR:-$HOME/.local/bin}"
image="ghcr.io/dahl-jar/jobranger/cli"
docker_docs="https://github.com/dahl-jar/jobranger/tree/main/dotnet/cli#docker"

fail() {
  echo "jobranger install: $1" >&2
  exit 1
}

detect_rid() {
  platform="$(uname -s) $(uname -m)"
  case "$platform" in
    "Linux x86_64") echo linux-x64 ;;
    Darwin\ *) fail "no macOS build, run jobranger in Docker with the image $image, see $docker_docs" ;;
    *) fail "no build for $platform, builds exist for Linux x64" ;;
  esac
}

download() {
  curl -fsSL --proto '=https,file' --proto-redir '=https' -o "$2" "$1"
}

on_path() {
  rest="$PATH:"
  while [ -n "$rest" ]; do
    entry="${rest%%:*}"
    rest="${rest#*:}"
    [ "$entry" -ef "$1" ] && return 0
  done
  return 1
}

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1"
  else
    shasum -a 256 "$1"
  fi | awk '{ print $1 }'
}

command -v curl >/dev/null 2>&1 || fail "curl is required"
command -v sha256sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1 || fail "no checksum tool found, sha256sum or shasum is required"

rid="$(detect_rid)"
archive="jobranger-$version-$rid.tar.gz"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

download "$download_url/$archive" "$tmp/$archive"
download "$download_url/SHA256SUMS" "$tmp/SHA256SUMS"

expected="$(awk -v name="$archive" '$2 == name { print $1 }' "$tmp/SHA256SUMS")"
[ -n "$expected" ] || fail "SHA256SUMS has no entry for $archive"
[ "$(sha256 "$tmp/$archive")" = "$expected" ] || fail "checksum mismatch for $archive"

tar -xzf "$tmp/$archive" -C "$tmp" jobranger
mkdir -p "$install_dir"
cp "$tmp/jobranger" "$install_dir/.jobranger.new"
chmod 0755 "$install_dir/.jobranger.new"
mv -f "$install_dir/.jobranger.new" "$install_dir/jobranger"

echo "jobranger $version installed to $install_dir/jobranger"
on_path "$install_dir" || echo "add $install_dir to PATH to run jobranger"
active="$(command -v jobranger || true)"
if [ -n "$active" ] && [ ! "$active" -ef "$install_dir/jobranger" ]; then
  echo "$active runs first on PATH, remove it or install again with JOBRANGER_INSTALL_DIR=\"${active%/*}\""
fi
